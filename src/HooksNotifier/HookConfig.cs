using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HooksNotifier;

internal static class HookConfig
{
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "settings.json");

    /// <summary>All supported hook events with their level.</summary>
    public static Dictionary<string, string> AllHooks { get; } = new()
    {
        ["Notification(idle_prompt)"]        = "P0",
        ["Notification(permission_prompt)"]  = "P0",
        ["StopFailure"]                      = "P0",
        ["Stop"]                             = "P0.5",
        ["TaskCompleted"]                    = "P0.5",
        ["SessionEnd"]                       = "P0.5",
        ["Notification(auth_success)"]       = "P1",
        ["Notification(elicitation_dialog)"] = "P1",
        ["Notification(elicitation_complete)"]="P1",
        ["PermissionDenied"]                 = "P1",
        ["PostToolUse"]                      = "P1",
        ["PostToolUseFailure"]               = "P1",
        ["SubagentStop"]                     = "P1",
        ["SessionStart"]                     = "P1",
        ["PostCompact"]                      = "P1",
        ["ConfigChange"]                     = "P1",
        ["SubagentStart"]                    = "P2",
        ["TaskCreated"]                      = "P2",
        ["PreCompact"]                       = "P2",
    };

    // ── Read from settings.json ─────────────────────────────────────

    /// <summary>Get the actual hook states from settings.json.</summary>
    public static Dictionary<string, bool> GetAllStates()
    {
        var result = new Dictionary<string, bool>();
        foreach (var key in AllHooks.Keys)
            result[key] = false;

        var active = ReadActiveFromSettings();
        foreach (var key in AllHooks.Keys)
        {
            var (eventName, matcher) = ParseKey(key);
            if (active.Contains((eventName, matcher)))
                result[key] = true;
        }
        return result;
    }

    /// <summary>Read all hook entries from settings.json. Returns (eventName, matcher) pairs.</summary>
    private static HashSet<(string, string)> ReadActiveFromSettings()
    {
        var active = new HashSet<(string, string)>();
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path)) return active;

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("hooks", out var hooks)) return active;

            foreach (var hookEvent in hooks.EnumerateObject())
            {
                foreach (var entry in hookEvent.Value.EnumerateArray())
                {
                    var matcher = entry.TryGetProperty("matcher", out var m) ? m.GetString() ?? "" : "";
                    // Check if this entry points to hooks-notifier.exe
                    var isOurs = false;
                    if (entry.TryGetProperty("hooks", out var hArray))
                    {
                        foreach (var h in hArray.EnumerateArray())
                        {
                            if (h.TryGetProperty("command", out var cmd) &&
                                cmd.GetString()?.Contains("hooks-notifier.exe") == true)
                            { isOurs = true; break; }
                        }
                    }
                    active.Add((hookEvent.Name, matcher));
                    _ = isOurs; // currently unused but available for future filtering
                }
            }
        }
        catch { }
        return active;
    }

    /// <summary>Enable a hook: run --configure-hooks to register all hooks.</summary>
    public static void Enable(string key)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--configure-hooks")
            { UseShellExecute = false, CreateNoWindow = true });
            proc?.WaitForExit(10000);
        }
        catch { }
    }

    /// <summary>Disable a hook: remove it from settings.json.</summary>
    public static void Disable(string key)
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path)) return;

            var (eventName, matcher) = ParseKey(key);
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "hooks")
                {
                    writer.WritePropertyName("hooks");
                    writer.WriteStartObject();
                    foreach (var hookEvent in prop.Value.EnumerateObject())
                    {
                        if (hookEvent.Name == eventName)
                        {
                            // Filter out entries matching our matcher
                            var remaining = new List<JsonElement>();
                            foreach (var entry in hookEvent.Value.EnumerateArray())
                            {
                                var m = entry.TryGetProperty("matcher", out var mt) ? mt.GetString() ?? "" : "";
                                if (m != matcher) remaining.Add(entry);
                            }
                            if (remaining.Count > 0)
                            {
                                writer.WritePropertyName(hookEvent.Name);
                                writer.WriteStartArray();
                                foreach (var e in remaining) e.WriteTo(writer);
                                writer.WriteEndArray();
                            }
                        }
                        else
                        {
                            writer.WritePropertyName(hookEvent.Name);
                            hookEvent.Value.WriteTo(writer);
                        }
                    }
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
            writer.Flush();

            File.WriteAllText(path, Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch { }
    }

    /// <summary>Parse "EventName(matcher)" into (eventName, matcher).</summary>
    private static (string eventName, string matcher) ParseKey(string key)
    {
        var match = Regex.Match(key, @"^(\w+)\((.+)\)$");
        if (match.Success)
            return (match.Groups[1].Value, match.Groups[2].Value);
        return (key, "");
    }
}
