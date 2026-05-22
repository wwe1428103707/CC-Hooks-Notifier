using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace HooksNotifier;

/// <summary>
/// Entry point. Dispatches to --hook, --tray, --register, or --configure-hooks mode.
/// Compiled as WinExe so --tray mode has no console window.
/// </summary>
internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    private static int Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "--hook";

        if (mode == "--tray")
            return TrayMode.Run();

        AttachConsole(ATTACH_PARENT_PROCESS);

        return mode switch
        {
            "--hook"            => HookMode.Run(),
            "--register"        => HookMode.RunRegister(),
            "--configure-hooks" => ConfigureHooks(),
            _                   => PrintUsage()
        };
    }

    private static int PrintUsage()
    {
        Console.Error.WriteLine("""
            hooks-notifier — Claude Code Hooks Notifier

            Usage:
              hooks-notifier                    (defaults to --hook)
              hooks-notifier --hook             Process a hook event (stdin JSON, stdout JSON)
              hooks-notifier --tray             Start background tray process
              hooks-notifier --register         Register AUMID for toast notifications
              hooks-notifier --configure-hooks  Update Claude Code settings.json to point to this EXE
            """);
        return 0;
    }

    /// <summary>
    /// Auto-configure hooks in ~/.claude/settings.json to point to this executable.
    /// Uses proper JSON parsing so escaped characters (\\, \") are handled correctly.
    /// </summary>
    private static int ConfigureHooks()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            Console.Error.WriteLine("ERROR: Cannot determine executable path.");
            return 1;
        }

        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "settings.json");

        if (!File.Exists(settingsPath))
        {
            Console.Error.WriteLine($"Settings not found: {settingsPath}");
            return 1;
        }

        try
        {
            // Parse settings.json into a mutable document
            var jsonContent = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Build the new command value (quoted path handles spaces)
            var newCommand = $"\"{exePath}\"";

            // Serialize to a stream so we can modify it
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            var count = WriteWithUpdatedCommands(writer, root, newCommand);
            writer.Flush();

            if (count == 0)
            {
                Console.Error.WriteLine("No existing hooks-notifier hooks found to update.");
                return 1;
            }

            var outputJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            File.WriteAllText(settingsPath, outputJson);
            Console.Error.WriteLine($"Updated {count} hook(s) to: {newCommand}");
            Console.Error.WriteLine($"Settings file: {settingsPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Recursively walk the JSON tree and update command values that reference hooks-notifier.exe.
    /// Returns the count of replacements made.
    /// </summary>
    private static int WriteWithUpdatedCommands(Utf8JsonWriter writer, JsonElement element, string newCommand)
    {
        var count = 0;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    // If this is a "command" property with a hooks-notifier.exe value
                    if (prop.Name == "command" && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.Value.GetString() ?? "";
                        if (val.Contains("hooks-notifier.exe"))
                        {
                            writer.WriteString(prop.Name, newCommand);
                            count++;
                            continue;
                        }
                    }

                    writer.WritePropertyName(prop.Name);
                    count += WriteWithUpdatedCommands(writer, prop.Value, newCommand);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    count += WriteWithUpdatedCommands(writer, item, newCommand);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }

        return count;
    }
}
