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
            var jsonContent = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            var newCommand = $"\"{exePath}\"";

            // Define all supported hook events and their matchers
            var allHooks = new Dictionary<string, string[]>
            {
                ["PermissionRequest"]   = [""],
                ["Notification"]        = ["idle_prompt", "permission_prompt", "auth_success", "elicitation_dialog", "elicitation_complete"],
                ["StopFailure"]         = [""],
                ["PermissionDenied"]    = [""],
                ["PostToolUse"]         = ["Edit|Write"],
                ["PostToolUseFailure"]  = ["Bash|Edit"],
                ["SubagentStart"]       = [""],
                ["SubagentStop"]        = [""],
                ["TaskCreated"]         = [""],
                ["TaskCompleted"]       = [""],
                ["Stop"]                = [""],
                ["SessionEnd"]          = [""],
                ["SessionStart"]        = [""],
                ["PreCompact"]          = [""],
                ["PostCompact"]         = [""],
                ["ConfigChange"]        = [""],
            };

            using var stream = new MemoryStream();
            var opts = new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            using var writer = new Utf8JsonWriter(stream, opts);

            int updated = 0, added = 0;
            writer.WriteStartObject();

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "hooks")
                {
                    writer.WritePropertyName("hooks");
                    writer.WriteStartObject();

                    var existingHooks = new HashSet<string>();
                    foreach (var hookEvent in prop.Value.EnumerateObject())
                    {
                        existingHooks.Add(hookEvent.Name);
                        writer.WritePropertyName(hookEvent.Name);

                        // Check if this hook event needs path update
                        var entries = hookEvent.Value;
                        var needsUpdate = false;
                        foreach (var entry in entries.EnumerateArray())
                        {
                            if (entry.TryGetProperty("hooks", out var hArray))
                            {
                                foreach (var h in hArray.EnumerateArray())
                                {
                                    if (h.TryGetProperty("command", out var cmd) &&
                                        cmd.GetString()?.Contains("hooks-notifier.exe") == true)
                                    {
                                        needsUpdate = true;
                                        break;
                                    }
                                }
                            }
                            if (needsUpdate) break;
                        }

                        if (needsUpdate)
                        {
                            // Rewrite with updated command
                            writer.WriteStartArray();
                            foreach (var entry in entries.EnumerateArray())
                            {
                                writer.WriteStartObject();
                                foreach (var eProp in entry.EnumerateObject())
                                {
                                    if (eProp.Name == "hooks")
                                    {
                                        writer.WritePropertyName("hooks");
                                        writer.WriteStartArray();
                                        foreach (var h in eProp.Value.EnumerateArray())
                                        {
                                            writer.WriteStartObject();
                                            foreach (var hProp in h.EnumerateObject())
                                            {
                                                if (hProp.Name == "command" &&
                                                    hProp.Value.GetString()?.Contains("hooks-notifier.exe") == true)
                                                {
                                                    writer.WriteString("command", newCommand);
                                                    updated++;
                                                }
                                                else
                                                {
                                                    WriteProp(writer, hProp);
                                                }
                                            }
                                            writer.WriteEndObject();
                                        }
                                        writer.WriteEndArray();
                                    }
                                    else if (eProp.Name == "matcher")
                                    {
                                        writer.WriteString("matcher", eProp.Value.GetString());
                                    }
                                    else
                                    {
                                        WriteProp(writer, eProp);
                                    }
                                }
                                writer.WriteEndObject();
                            }
                            writer.WriteEndArray();
                        }
                        else
                        {
                            // Pass through unchanged
                            hookEvent.Value.WriteTo(writer);
                        }

                        existingHooks.Add(hookEvent.Name);
                    }

                    // Add missing hook events
                    foreach (var kv in allHooks)
                    {
                        if (existingHooks.Contains(kv.Key)) continue;

                        writer.WritePropertyName(kv.Key);
                        writer.WriteStartArray();
                        foreach (var matcher in kv.Value)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("matcher", matcher);
                            writer.WritePropertyName("hooks");
                            writer.WriteStartArray();
                            writer.WriteStartObject();
                            writer.WriteString("type", "command");
                            writer.WriteString("command", newCommand);
                            writer.WriteEndObject();
                            writer.WriteEndArray();
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                        added++;
                    }

                    writer.WriteEndObject();
                }
                else
                {
                    WriteProp(writer, prop);
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            var outputJson = Encoding.UTF8.GetString(stream.ToArray());
            File.WriteAllText(settingsPath, outputJson);
            Console.Error.WriteLine($"Updated {updated} hook(s), added {added} new hook event(s).");
            Console.Error.WriteLine($"Command: {newCommand}");
            Console.Error.WriteLine($"Settings: {settingsPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Write a JSON property from a JsonProperty iteration.</summary>
    private static void WriteProp(Utf8JsonWriter writer, System.Text.Json.JsonProperty prop)
    {
        writer.WritePropertyName(prop.Name);
        prop.Value.WriteTo(writer);
    }
}
