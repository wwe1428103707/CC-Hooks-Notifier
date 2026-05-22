using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace NotifyHook;

/// <summary>
/// Lightweight hook handler — called by Claude Code hooks.
/// Shows WinRT toasts directly, no WinForms/WebView2 dependencies.
/// Fast startup, exits immediately after handling.
/// </summary>
sealed class Program
{
    private const string PipeName = "ClaudeCodeHooks";
    private const string Aumid = "ClaudeCode.HooksNotifier";

    static int Main()
    {
        // When double-clicked (no pipe), launch tray and exit
        if (!Console.IsInputRedirected)
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath);
            var trayExe = dir != null ? Path.Combine(dir, "hooks-notifier.exe") : null;
            if (trayExe != null && File.Exists(trayExe))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(trayExe, "--tray")
                { UseShellExecute = true });
            }
            return 0;
        }

        string rawJson;
        try { rawJson = Console.In.ReadToEnd(); }
        catch { return 1; }

        if (string.IsNullOrWhiteSpace(rawJson)) return 0;

        HookData? data;
        try { data = JsonSerializer.Deserialize<HookData>(rawJson, JsonOpts.Default); }
        catch { return 1; }

        if (data?.HookEventName == null) return 0;

        try
        {
            switch (data.HookEventName)
            {
                case "PermissionRequest":
                    // No interactive dialog — deny by default
                    var decision = new { behavior = "deny" };
                    var output = new
                    {
                        hookSpecificOutput = new
                        {
                            hookEventName = "PermissionRequest",
                            decision
                        }
                    };
                    Console.WriteLine(JsonSerializer.Serialize(output, JsonOpts.Default));
                    return 0;

                case "Notification":
                    ShowToast(data);
                    return 0;

                case "StopFailure":
                case "PermissionDenied":
                case "PostToolUse":
                case "PostToolUseFailure":
                case "SubagentStop":
                case "TaskCompleted":
                case "Stop":
                case "SessionEnd":
                case "SessionStart":
                case "PostCompact":
                case "ConfigChange":
                    ShowToast(data);
                    return 0;

                case "SubagentStart":
                case "TaskCreated":
                case "PreCompact":
                    // Stateful only — try IPC, ignore failure
                    TrySendIpc(data);
                    return 0;

                default:
                    return 0;
            }
        }
        catch { return 1; }
    }

    static void ShowToast(HookData data)
    {
        var (title, body) = FormatMessage(data);
        try
        {
            var template = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var nodes = template.GetElementsByTagName("text");
            nodes[0].AppendChild(template.CreateTextNode(title));
            nodes[1].AppendChild(template.CreateTextNode(body));
            var toast = new ToastNotification(template);
            ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
        }
        catch
        {
            // Silently fail — nothing else we can do without WinForms
        }

        // Also notify tray for event history (best-effort, non-blocking)
        NotifyTray(data, title, body);
    }

    /// <summary>Send event to tray for history logging (fire-and-forget).</summary>
    static void NotifyTray(HookData data, string title, string body)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            pipe.Connect(500);
            var msg = JsonSerializer.Serialize(new
            {
                type = "toast",
                title,
                body,
                eventName = data.HookEventName ?? "",
                eventType = data.HookEventType ?? "",
                blinkType = "none"
            }, JsonOpts.Default);
            var buf = Encoding.UTF8.GetBytes(msg + "\n");
            pipe.Write(buf, 0, buf.Length);
            pipe.Flush();
            // Read response to verify delivery
            using var reader = new StreamReader(pipe, Encoding.UTF8);
            reader.ReadLine();
        }
        catch (TimeoutException) { /* tray busy — skip */ }
        catch (FileNotFoundException) { /* tray not running — skip */ }
    }

    static (string title, string body) FormatMessage(HookData data)
    {
        var ev = data.HookEventName ?? "";
        var type = data.HookEventType ?? "";
        var sub = data.HookEventSubtype ?? "";
        var tool = data.ToolName ?? "";

        return ev switch
        {
            "Notification" => type switch
            {
                "idle_prompt"          => ("Claude Code", "Task complete — ready for your input"),
                "permission_prompt"    => ("Claude Code — Permission Needed", "Claude is waiting for approval"),
                "auth_success"         => ("Claude Code", "Authentication successful"),
                "elicitation_dialog"   => ("Claude Code — MCP Input", "MCP server needs your input"),
                "elicitation_complete" => ("Claude Code", "MCP input submitted"),
                _                      => ("Claude Code", $"Event: {type}"),
            },
            "StopFailure" => type switch
            {
                "rate_limit"            => ("Claude Code", "API rate limit reached"),
                "server_error"          => ("Claude Code", "API server error"),
                "authentication_failed" => ("Claude Code", "Authentication failed"),
                _                       => ("Claude Code", $"API error: {type}"),
            },
            "PermissionDenied" => ("Claude Code",
                tool.StartsWith("mcp__") ? $"MCP tool denied: {tool[5..]}" : $"Tool denied: {tool}"),
            "PostToolUse" => ("Claude Code", $"Edited: {ExtractPath(data)}"),
            "PostToolUseFailure" => ("Claude Code", tool == "Bash" ? "Command failed" : $"Tool failed: {tool}"),
            "SubagentStop" => ("Claude Code", $"Subagent finished: {type}"),
            "TaskCompleted" => ("Claude Code", $"Task completed: {sub}"),
            "Stop" => ("Claude Code", "Finished responding"),
            "SessionEnd" => ("Claude Code", "Session ended"),
            "SessionStart" => ("Claude Code", type == "startup" ? "Session started" : "Session resumed"),
            "PostCompact" => ("Claude Code", "Context compaction complete"),
            "ConfigChange" => ("Claude Code", $"{type} modified"),
            _ => ("Claude Code", $"Hook event: {ev}"),
        };
    }

    static string ExtractPath(HookData data)
    {
        if (data.ToolInput == null) return "";
        foreach (var key in new[] { "file_path", "filePath", "path" })
        {
            if (data.ToolInput.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            {
                var val = el.GetString();
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        return "";
    }

    static void TrySendIpc(HookData data)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            pipe.Connect(1000);
            var msg = JsonSerializer.Serialize(new
            {
                type = "stateful",
                title = $"{data.HookEventName}: {data.HookEventType}",
                eventName = data.HookEventName,
                eventType = data.HookEventType ?? "",
                blinkType = "none"
            }, JsonOpts.Default);
            var buf = Encoding.UTF8.GetBytes(msg + "\n");
            pipe.Write(buf, 0, buf.Length);
        }
        catch { /* tray not running — skip */ }
    }

    // ── JSON models ─────────────────────────────────────────────────
    sealed record HookData
    {
        [JsonPropertyName("hook_event_name")] public string? HookEventName { get; init; }
        [JsonPropertyName("hook_event_type")] public string? HookEventType { get; init; }
        [JsonPropertyName("hook_event_subtype")] public string? HookEventSubtype { get; init; }
        [JsonPropertyName("tool_name")] public string? ToolName { get; init; }
        [JsonPropertyName("tool_input")] public Dictionary<string, JsonElement>? ToolInput { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
    }

    static class JsonOpts
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}
