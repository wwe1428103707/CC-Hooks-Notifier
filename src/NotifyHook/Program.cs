using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace NotifyHook;

/// <summary>
/// Lightweight hook handler — called by Claude Code hooks.
/// Shows WinRT toasts + interactive permission dialogs.
/// Fast startup, exits after handling.
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

        Console.InputEncoding = Encoding.UTF8;
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
                    return HandlePermissionRequest(data);

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

    /// <summary>
    /// Handle PermissionRequest: show toast + interactive WinForms dialog,
    /// then output the user's allow/deny decision to stdout.
    /// Supports "always allow" via updatedPermissions.
    /// </summary>
    static int HandlePermissionRequest(HookData data)
    {
        ShowToast(data);

        var result = ShowPermissionDialog(data);

        var decision = new Dictionary<string, object?>
        {
            ["behavior"] = result.Allowed ? "allow" : "deny"
        };

        var hookOutput = new Dictionary<string, object?>
        {
            ["hookEventName"] = "PermissionRequest",
            ["decision"] = decision
        };

        // Add updatedPermissions if user selected any "always allow" options
        if (result.Allowed && result.SelectedPermissions.Count > 0)
        {
            hookOutput["updatedPermissions"] = result.SelectedPermissions;
        }

        var output = new Dictionary<string, object?>
        {
            ["hookSpecificOutput"] = hookOutput
        };

        Console.WriteLine(JsonSerializer.Serialize(output, JsonOpts.Default));

        var outcome = result.Allowed ? "allowed" : "denied";
        NotifyTray(data, "Claude Code", $"Permission {outcome}: {data.ToolName}");
        return 0;
    }

    sealed record PermissionDialogOutcome(bool Allowed, List<object> SelectedPermissions);

    /// <summary>Show a WinForms dialog with Allow/Deny and optional "always allow" checkboxes.</summary>
    static PermissionDialogOutcome ShowPermissionDialog(HookData data)
    {
        var tool = data.ToolName ?? "Unknown";

        var detailLines = new List<string>();
        if (data.ToolInput != null)
        {
            foreach (var kv in data.ToolInput)
            {
                if (string.Equals(kv.Key, "description", StringComparison.OrdinalIgnoreCase))
                    continue;
                detailLines.Add($"{kv.Key}: {kv.Value}");
            }
        }

        // Parse permission_suggestions from input
        var suggestions = new List<JsonElement>();
        if (data.PermissionSuggestions != null)
        {
            foreach (var s in data.PermissionSuggestions)
                suggestions.Add(s);
        }

        // Dialog dimensions: taller when suggestions exist
        var hasSuggestions = suggestions.Count > 0;
        var formH = hasSuggestions ? 420 : 340;
        var detailsH = hasSuggestions ? 120 : 150;
        var barY = hasSuggestions ? 324 : 244;

        using var form = new Form
        {
            Text = "Claude Code — Permission Required",
            Size = new Size(560, formH),
            StartPosition = FormStartPosition.CenterScreen,
            TopMost = true,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(248, 249, 250),
            Font = new Font("Microsoft YaHei UI", 9)
        };

        form.Controls.Add(new Label
        {
            Text = "Claude Code needs your permission",
            Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(20, 14),
            Size = new Size(520, 26)
        });

        form.Controls.Add(new Label
        {
            Text = $"Tool: {tool}",
            Font = new Font("Consolas", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(67, 97, 238),
            Location = new Point(20, 46),
            Size = new Size(520, 18)
        });

        form.Controls.Add(new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9),
            Location = new Point(20, 72),
            Size = new Size(510, detailsH),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Text = detailLines.Count > 0 ? string.Join("\r\n", detailLines) : "(no details)",
            TabStop = false
        });

        // "Always allow" checkboxes from permission_suggestions
        var checkboxes = new List<CheckBox>();
        if (hasSuggestions)
        {
            var yOff = 72 + detailsH + 8;
            form.Controls.Add(new Label
            {
                Text = "Always allow:",
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(20, yOff),
                Size = new Size(100, 20)
            });

            for (int i = 0; i < suggestions.Count; i++)
            {
                var cb = new CheckBox
                {
                    Text = DescribeSuggestion(suggestions[i], tool),
                    Location = new Point(30, yOff + 22 + i * 24),
                    Size = new Size(500, 22),
                    Font = new Font("Microsoft YaHei UI", 9),
                    ForeColor = Color.FromArgb(50, 50, 50),
                    TabIndex = 10 + i,
                    Checked = false
                };
                form.Controls.Add(cb);
                checkboxes.Add(cb);
            }
        }

        var bar = new Panel
        {
            Location = new Point(0, barY),
            Size = new Size(560, 50),
            BackColor = Color.FromArgb(241, 243, 245),
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(bar);

        var btnDeny = new Button
        {
            Text = "Deny",
            Location = new Point(440, 10),
            Size = new Size(95, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(220, 53, 69),
            DialogResult = DialogResult.No,
            TabIndex = 2
        };
        btnDeny.FlatAppearance.BorderColor = Color.FromArgb(220, 53, 69);
        bar.Controls.Add(btnDeny);

        var btnAllow = new Button
        {
            Text = "Allow",
            Location = new Point(330, 10),
            Size = new Size(95, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(67, 97, 238),
            ForeColor = Color.White,
            DialogResult = DialogResult.Yes,
            TabIndex = 0
        };
        btnAllow.FlatAppearance.BorderColor = Color.FromArgb(67, 97, 238);
        bar.Controls.Add(btnAllow);

        form.AcceptButton = btnAllow;
        form.CancelButton = btnDeny;

        var dialogResult = form.ShowDialog();
        var allowed = dialogResult == DialogResult.Yes;

        // Collect checked suggestions as updatedPermissions
        var selectedPerms = new List<object>();
        if (allowed)
        {
            for (int i = 0; i < checkboxes.Count; i++)
            {
                if (checkboxes[i].Checked)
                {
                    selectedPerms.Add(suggestions[i]);
                }
            }
        }

        return new PermissionDialogOutcome(allowed, selectedPerms);
    }

    /// <summary>Generate a human-readable label for a permission suggestion.</summary>
    static string DescribeSuggestion(JsonElement s, string tool)
    {
        var type = s.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        var behavior = s.TryGetProperty("behavior", out var b) ? b.GetString() ?? "" : "";
        var dest = s.TryGetProperty("destination", out var d) ? d.GetString() ?? "" : "";

        if (type == "addRules" && s.TryGetProperty("rules", out var rules))
        {
            foreach (var rule in rules.EnumerateArray())
            {
                var tn = rule.TryGetProperty("toolName", out var rn) ? rn.GetString() ?? "" : "";
                var rc = rule.TryGetProperty("ruleContent", out var rv) ? rv.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(rc))
                    return $"\"{rc}\" ({tn})";
                if (!string.IsNullOrEmpty(tn))
                    return $"All {tn} operations";
            }
        }

        if (type == "setMode")
        {
            var mode = s.TryGetProperty("mode", out var m) ? m.GetString() ?? "" : "";
            return $"Set permission mode to \"{mode}\"";
        }

        if (type == "addDirectories")
        {
            var dirs = s.TryGetProperty("directories", out var dirArr) ? dirArr.EnumerateArray().Count() : 0;
            return $"Add {dirs} working director{(dirs == 1 ? "y" : "ies")} to whitelist";
        }

        return $"{type} ({behavior}, {dest})";
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

        // Use the original untruncated content as detail when available
        var detail = data.LastAssistantMessage ?? data.CompactSummary ?? data.ErrorDetails ?? data.Error ?? data.Reason ?? "";
        // Also notify tray for event history (best-effort, non-blocking)
        NotifyTray(data, title, body, detail);
    }

    /// <summary>Send event to tray for history logging (fire-and-forget).</summary>
    static void NotifyTray(HookData data, string title, string body, string detail = "")
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            pipe.Connect(2000);
            var msg = JsonSerializer.Serialize(new
            {
                type = "toast",
                title,
                body,
                detail,
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
            "Notification" => FormatNotification(data, type),
            "StopFailure" => FormatStopFailure(data, type),
            "PermissionRequest" => ("Claude Code — Permission Needed",
                string.IsNullOrEmpty(tool) ? "Claude is waiting for your approval" : $"Claude needs permission to use: {tool}"),
            "PermissionDenied" => FormatPermissionDenied(data, tool),
            "PostToolUse" => ("Claude Code", $"Edited: {ExtractPath(data)}"),
            "PostToolUseFailure" => FormatPostToolUseFailure(data, tool),
            "SubagentStop" => FormatSubagentStop(data, type),
            "TaskCompleted" => FormatTaskCompleted(data, sub),
            "Stop" => FormatStop(data),
            "SessionEnd" => FormatSessionEnd(data),
            "SessionStart" => FormatSessionStart(data, type),
            "PostCompact" => FormatPostCompact(data),
            "ConfigChange" => FormatConfigChange(data, type),
            _ => ("Claude Code", $"Hook event: {ev}"),
        };
    }

    static (string title, string body) FormatNotification(HookData data, string type)
    {
        // Prefer the message field from Claude Code when available
        if (!string.IsNullOrEmpty(data.Message))
        {
            var t = !string.IsNullOrEmpty(data.Title) ? data.Title : "Claude Code";
            return (t, data.Message);
        }

        // Fall back to hardcoded descriptions
        return type switch
        {
            "idle_prompt"          => ("Claude Code", "Task complete — ready for your input"),
            "permission_prompt"    => ("Claude Code — Permission Needed", "Claude is waiting for approval"),
            "auth_success"         => ("Claude Code", "Authentication successful"),
            "elicitation_dialog"   => ("Claude Code — MCP Input", "MCP server needs your input"),
            "elicitation_complete" => ("Claude Code", "MCP input submitted"),
            _                      => ("Claude Code", $"Event: {type}"),
        };
    }

    static (string title, string body) FormatPostToolUseFailure(HookData data, string tool)
    {
        // Use the error field when available
        if (!string.IsNullOrEmpty(data.Error))
            return ("Claude Code", $"{tool}: {data.Error}");

        var desc = data.Description;
        if (!string.IsNullOrEmpty(desc))
            return ("Claude Code", $"{tool}: {desc}");

        return ("Claude Code", tool == "Bash" ? "Command failed" : $"Tool failed: {tool}");
    }

    static (string title, string body) FormatStopFailure(HookData data, string type)
    {
        // Use error_details when available (most specific)
        if (!string.IsNullOrEmpty(data.ErrorDetails))
            return ("Claude Code", data.ErrorDetails);

        // Then try the error field
        if (!string.IsNullOrEmpty(data.Error))
        {
            return ("Claude Code", type switch
            {
                "rate_limit"            => "API rate limit reached",
                "server_error"          => "API server error",
                "authentication_failed" => "Authentication failed",
                _                       => $"API error: {data.Error}",
            });
        }

        return ("Claude Code", type switch
        {
            "rate_limit"            => "API rate limit reached",
            "server_error"          => "API server error",
            "authentication_failed" => "Authentication failed",
            _                       => $"API error: {type}",
        });
    }

    static (string title, string body) FormatPermissionDenied(HookData data, string tool)
    {
        // Show the reason when available
        if (!string.IsNullOrEmpty(data.Reason))
        {
            var toolLabel = tool.StartsWith("mcp__") ? tool[5..] : tool;
            return ("Claude Code", $"{toolLabel}: {data.Reason}");
        }

        return ("Claude Code",
            tool.StartsWith("mcp__") ? $"MCP tool denied: {tool[5..]}" : $"Tool denied: {tool}");
    }

    static (string title, string body) FormatPostCompact(HookData data)
    {
        if (!string.IsNullOrEmpty(data.CompactSummary))
            return ("Claude Code", Truncate(data.CompactSummary, 120));
        return ("Claude Code", "Context compaction complete");
    }

    static (string title, string body) FormatSubagentStop(HookData data, string type)
    {
        if (!string.IsNullOrEmpty(data.LastAssistantMessage))
            return ("Claude Code", $"{type}: {Truncate(data.LastAssistantMessage, 80)}");
        return ("Claude Code", $"Subagent finished: {type}");
    }

    static (string title, string body) FormatStop(HookData data)
    {
        if (!string.IsNullOrEmpty(data.LastAssistantMessage))
            return ("Claude Code", Truncate(data.LastAssistantMessage, 100));
        return ("Claude Code", "Finished responding");
    }

    static (string title, string body) FormatTaskCompleted(HookData data, string sub)
    {
        if (!string.IsNullOrEmpty(data.TaskSubject))
            return ("Claude Code", $"Task done: {Truncate(data.TaskSubject, 80)}");
        if (!string.IsNullOrEmpty(sub))
            return ("Claude Code", $"Task completed: {sub}");
        return ("Claude Code", "Task completed");
    }

    static (string title, string body) FormatSessionStart(HookData data, string type)
    {
        var label = type == "startup" ? "Session started" : "Session resumed";
        if (!string.IsNullOrEmpty(data.Model))
            return ("Claude Code", $"{label} ({data.Model})");
        return ("Claude Code", label);
    }

    static (string title, string body) FormatSessionEnd(HookData data)
    {
        if (!string.IsNullOrEmpty(data.Reason) && data.Reason != "other")
            return ("Claude Code", $"Session ended: {data.Reason}");
        return ("Claude Code", "Session ended");
    }

    static (string title, string body) FormatConfigChange(HookData data, string type)
    {
        if (!string.IsNullOrEmpty(data.Source))
        {
            var src = data.Source switch
            {
                "project_settings" => "project",
                "user_settings" => "user",
                _ => data.Source
            };
            return ("Claude Code", $"Settings changed ({src})");
        }
        return ("Claude Code", $"{type} modified");
    }

    static string Truncate(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= maxLen) return s;
        return s[..maxLen] + "...";
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
            var title = !string.IsNullOrEmpty(data.TaskSubject)
                ? $"Task: {Truncate(data.TaskSubject, 60)}"
                : $"{data.HookEventName}: {data.HookEventType}";
            var msg = JsonSerializer.Serialize(new
            {
                type = "stateful",
                title,
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
        [JsonPropertyName("permission_suggestions")] public JsonElement[]? PermissionSuggestions { get; init; }
        [JsonPropertyName("message")] public string? Message { get; init; }
        [JsonPropertyName("title")] public string? Title { get; init; }
        [JsonPropertyName("notification_type")] public string? NotificationType { get; init; }
        [JsonPropertyName("error")] public string? Error { get; init; }
        [JsonPropertyName("error_details")] public string? ErrorDetails { get; init; }
        [JsonPropertyName("reason")] public string? Reason { get; init; }
        [JsonPropertyName("compact_summary")] public string? CompactSummary { get; init; }
        [JsonPropertyName("last_assistant_message")] public string? LastAssistantMessage { get; init; }
        [JsonPropertyName("task_subject")] public string? TaskSubject { get; init; }
        [JsonPropertyName("source")] public string? Source { get; init; }
        [JsonPropertyName("model")] public string? Model { get; init; }
    }

    static class JsonOpts
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}
