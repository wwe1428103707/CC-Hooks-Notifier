using System.Text.Json;

namespace HooksNotifier;

/// <summary>
/// --hook mode: called by Claude Code hooks via stdin/stdout.
/// Processes the event and shows notifications or permission dialogs.
/// </summary>
internal static class HookMode
{
    /// <summary>Entry point for --hook mode.</summary>
    public static int Run()
    {
        // Read stdin
        string rawJson;
        try { rawJson = Console.In.ReadToEnd(); }
        catch (Exception ex) { Log.Error($"stdin: {ex.Message}"); return 1; }

        if (string.IsNullOrWhiteSpace(rawJson)) return 0;

        HookData? data;
        try { data = JsonSerializer.Deserialize<HookData>(rawJson, JsonOpts.Default); }
        catch (Exception ex) { Log.Error($"JSON: {ex.Message}"); return 1; }

        if (data?.HookEventName == null) return 0;

        return HandleDirect(data);
    }

    /// <summary>Handle event directly (no tray process available).</summary>
    private static int HandleDirect(HookData data)
    {
        return data.HookEventName switch
        {
            "PermissionRequest" => HandlePermissionRequest(data),
            "Notification"      => HandleNotification(data),
            "StopFailure"       => HandleStopFailure(data),
            "PermissionDenied"   => HandlePermissionDenied(data),
            "PostToolUse"        => HandlePostToolUse(data),
            "PostToolUseFailure" => HandlePostToolUseFailure(data),
            "SubagentStart"      => HandleSubagentStart(data),
            "SubagentStop"       => HandleSubagentStop(data),
            "TaskCreated"        => HandleTaskCreated(data),
            "Stop"               => HandleStop(data),
            "TaskCompleted"      => HandleTaskCompleted(data),
            "SessionEnd"         => HandleSessionEnd(data),
            "SessionStart"       => HandleSessionStart(data),
            "PreCompact"         => HandlePreCompact(data),
            "PostCompact"        => HandlePostCompact(data),
            "ConfigChange"       => HandleConfigChange(data),
            _                    => HandleDefault(data)
        };
    }

    // ── Notification event ─────────────────────────────────────────────
    private static int HandleNotification(HookData data)
    {
        var (title, body, blinkType) = FormatNotification(data);
        if (TrySendIpc(data.HookEventType, title, body, blinkType))
            return 0;

        ToastService.Show(title, body);
        return 0;
    }

    public static (string title, string body, string blinkType) FormatNotification(HookData data)
    {
        var eventType = data.HookEventType ?? "";

        var (title, body, blinkType) = eventType switch
        {
            "idle_prompt"         => ("Claude Code", "Task complete — ready for your input", "long"),
            "permission_prompt"   => ("Claude Code — Permission Needed",
                "Claude is waiting for you to approve a tool call", "long"),
            "auth_success"        => ("Claude Code", "Authentication successful", "none"),
            "elicitation_dialog"  => ("Claude Code — MCP Input Requested",
                "An MCP server needs your input", "none"),
            "elicitation_complete"=> ("Claude Code", "MCP input submitted", "none"),
            _                     => ("Claude Code", $"Notification: {eventType}", "none")
        };
        return (title, body, blinkType);
    }

    /// <summary>Try sending via IPC to tray. Returns true if tray handled it.</summary>
    private static bool TrySendIpc(string? eventType, string title, string body, string blinkType, string eventName = "Notification")
    {
        try
        {
            var msg = new IpcMessage
            {
                Type = "toast",
                Title = title,
                Body = body,
                EventName = eventName,
                EventType = eventType ?? "",
                BlinkType = blinkType
            };
            return IpcService.Send(msg);
        }
        catch
        {
            return false;
        }
    }

    // ── StopFailure event ─────────────────────────────────────────────
    private static int HandleStopFailure(HookData data)
    {
        var eventType = data.HookEventType ?? "unknown";
        var (title, body, blinkType) = eventType switch
        {
            "rate_limit"            => ("Claude Code — Rate Limited",
                "API rate limit reached. Claude may retry shortly.", "long"),
            "server_error"          => ("Claude Code — Server Error",
                "Claude API encountered a server error.", "long"),
            "authentication_failed" => ("Claude Code — Auth Failed",
                "Authentication failed. Check your API credentials.", "long"),
            "billing_error"         => ("Claude Code — Billing Error",
                "There is a billing issue with your API account.", "none"),
            "max_output_tokens"     => ("Claude Code",
                "Response was truncated (max output tokens reached).", "none"),
            "model_not_found"       => ("Claude Code",
                "Requested model is not available.", "none"),
            _                       => ("Claude Code",
                $"API error: {eventType}", "none")
        };

        if (TrySendIpc(eventType, title, body, blinkType, "StopFailure"))
            return 0;

        ToastService.Show(title, body);
        return 0;
    }

    // ── Permission denied event ────────────────────────────────────────
    private static int HandlePermissionDenied(HookData data)
    {
        var toolName = data.ToolName ?? "Unknown";
        var (title, body, _) = toolName switch
        {
            _ when toolName.StartsWith("mcp__") => ("Claude Code",
                $"MCP tool denied: {toolName[5..]}", "none"),
            _ => ("Claude Code",
                $"Tool call denied: {toolName}", "none")
        };

        if (TrySendIpc(null, title, body, "none", "PermissionDenied"))
            return 0;

        ToastService.Show(title, body);
        return 0;
    }

    // ── PostToolUse event ─────────────────────────────────────────────
    private static int HandlePostToolUse(HookData data)
    {
        var toolName = data.ToolName ?? "Unknown";
        var subType = data.HookEventSubtype ?? "";
        var filePath = ExtractFilePath(data);

        if (string.IsNullOrEmpty(filePath))
            return 0; // No file path to report — skip

        var title = "Claude Code";
        var body = $"Edited: {filePath}";

        if (TrySendIpc(null, title, body, "none", "PostToolUse"))
            return 0;

        ToastService.Show(title, body);
        return 0;
    }

    // ── PostToolUseFailure event ───────────────────────────────────────
    private static int HandlePostToolUseFailure(HookData data)
    {
        var toolName = data.ToolName ?? "Unknown";
        var subType = data.HookEventSubtype ?? "";

        var title = "Claude Code";
        var body = toolName switch
        {
            "Bash" => "Command failed — see terminal for details",
            _      => $"Tool failed: {toolName}"
        };

        if (TrySendIpc(null, title, body, "none", "PostToolUseFailure"))
            return 0;

        ToastService.Show(title, body);
        return 0;
    }

    /// <summary>Extract file_path from tool_input for PostToolUse events.</summary>
    private static string ExtractFilePath(HookData data)
    {
        if (data.ToolInput == null) return "";

        // Try common file path fields
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

    // ── SubagentStart event ──────────────────────────────────────────
    private static int HandleSubagentStart(HookData data)
    {
        var agentType = data.HookEventType ?? "unknown";
        TrySendStateful("SubagentStart", agentType, $"Subagent started: {agentType}");
        return 0; // stateful-only — no toast fallback needed
    }

    // ── SubagentStop event ───────────────────────────────────────────
    private static int HandleSubagentStop(HookData data)
    {
        var agentType = data.HookEventType ?? "unknown";
        var title = "Claude Code";
        var body = $"Subagent finished: {agentType}";

        if (TrySendIpc(null, title, body, "none", "SubagentStop"))
            return 0;

        ToastService.Show(title, body);
        return 0;
    }

    // ── TaskCreated event ────────────────────────────────────────────
    private static int HandleTaskCreated(HookData data)
    {
        var desc = data.HookEventSubtype ?? data.Description ?? "new task";
        TrySendStateful("TaskCreated", desc, $"Task created: {desc}");
        return 0; // stateful-only — no toast fallback needed
    }

    /// <summary>Send a stateful IPC update (tray status only, no toast).</summary>
    private static void TrySendStateful(string eventName, string eventType, string description)
    {
        try
        {
            var msg = new IpcMessage
            {
                Type = "stateful",
                Title = description,
                EventName = eventName,
                EventType = eventType,
                BlinkType = "none"
            };
            IpcService.Send(msg);
        }
        catch
        {
            // Tray not running — stateful update silently skipped
        }
    }

    // ── Permission request event ───────────────────────────────────────
    private static int HandlePermissionRequest(HookData data)
    {
        var toolName = data.ToolName ?? "Unknown";

        ToastService.Show("Claude Code — Permission Required", $"Tool: {toolName}");
        var decision = ShowPermissionDialog(toolName, data);
        ToastService.Show(
            "Claude Code — Permission " + (decision.Behavior == "allow" ? "Allowed" : "Denied"),
            $"Tool: {toolName}");

        var output = new HookOutput
        {
            HookSpecificOutput = new HookSpecificOutput
            {
                HookEventName = "PermissionRequest",
                Decision = decision
            }
        };
        Console.WriteLine(JsonSerializer.Serialize(output, JsonOpts.Default));
        return 0;
    }

    // ── Stop event (P0.5 short blink) ─────────────────────────────────
    private static int HandleStop(HookData data)
    {
        var title = "Claude Code";
        var body = "Finished responding";
        if (TrySendIpc(null, title, body, "short", "Stop"))
            return 0;
        ToastService.Show(title, body);
        return 0;
    }

    // ── TaskCompleted event (P0.5 short blink) ────────────────────────
    private static int HandleTaskCompleted(HookData data)
    {
        var desc = data.HookEventSubtype ?? data.Description ?? "task";
        var title = "Claude Code";
        var body = $"Task completed: {desc}";
        if (TrySendIpc(null, title, body, "short", "TaskCompleted"))
            return 0;
        ToastService.Show(title, body);
        return 0;
    }

    // ── SessionEnd event (P0.5 short blink for key reasons) ───────────
    private static int HandleSessionEnd(HookData data)
    {
        var eventType = data.HookEventType ?? "";
        // Only blink for user-initiated session ends, not background events
        var blinkType = eventType switch
        {
            "clear" or "logout" or "prompt_input_exit" => "short",
            _ => "none"
        };
        var title = "Claude Code";
        var body = blinkType == "short" ? "Session ended" : $"Session: {eventType}";
        if (TrySendIpc(null, title, body, blinkType, "SessionEnd"))
            return 0;
        ToastService.Show(title, body);
        return 0;
    }

    // ── SessionStart event ─────────────────────────────────────────────
    private static int HandleSessionStart(HookData data)
    {
        var eventType = data.HookEventType ?? "";

        switch (eventType)
        {
            case "startup":
            case "resume":
                var title = "Claude Code";
                var body = eventType == "startup" ? "Session started" : "Session resumed";
                if (!TrySendIpc(null, title, body, "none", "SessionStart"))
                    ToastService.Show(title, body);
                break;

            default:
                // clear/compact — silent stateful update only
                TrySendStateful("SessionStart", eventType, $"Session: {eventType}");
                break;
        }
        return 0;
    }

    // ── PreCompact event (stateful only) ──────────────────────────────
    private static int HandlePreCompact(HookData data)
    {
        var eventType = data.HookEventType ?? "auto";
        TrySendStateful("PreCompact", eventType, $"Compacting context...");
        return 0;
    }

    // ── PostCompact event (toast) ─────────────────────────────────────
    private static int HandlePostCompact(HookData data)
    {
        var eventType = data.HookEventType ?? "auto";
        var title = "Claude Code";
        var body = "Context compaction complete";
        if (TrySendIpc(null, title, body, "none", "PostCompact"))
            return 0;
        ToastService.Show(title, body);
        return 0;
    }

    // ── ConfigChange event ────────────────────────────────────────────
    private static int HandleConfigChange(HookData data)
    {
        var source = data.HookEventType ?? "unknown";
        var filePath = "";
        if (data.ToolInput != null &&
            data.ToolInput.TryGetValue("file_path", out var el) &&
            el.ValueKind == JsonValueKind.String)
        {
            filePath = el.GetString() ?? "";
        }

        var sourceLabel = source switch
        {
            "user_settings"   => "User settings",
            "project_settings"=> "Project settings",
            "local_settings"  => "Local settings",
            "policy_settings" => "Policy settings",
            "skills"          => "Skills config",
            _                 => $"Config: {source}"
        };

        var title = "Claude Code";
        var body = string.IsNullOrEmpty(filePath)
            ? $"{sourceLabel} modified"
            : $"{sourceLabel}: {Path.GetFileName(filePath)}";

        if (TrySendIpc(null, title, body, "none", "ConfigChange"))
            return 0;
        ToastService.Show(title, body);
        return 0;
    }

    // ── Default event ──────────────────────────────────────────────────
    private static int HandleDefault(HookData data)
    {
        ToastService.Show("Claude Code", $"Hook event: {data.HookEventName}");
        return 0;
    }

    // ── Registration (Start Menu shortcut for AUMID) ───────────────────
    public static int RunRegister()
    {
        try
        {
            var appPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(appPath))
            {
                Console.Error.WriteLine("hooks-notifier: cannot determine process path");
                return 1;
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                Console.Error.WriteLine("hooks-notifier: WScript.Shell not available");
                return 1;
            }

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null)
            {
                Console.Error.WriteLine("hooks-notifier: failed to create WScript.Shell");
                return 1;
            }

            var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var shortcutPath = Path.Combine(startMenu, "Programs", "Claude Code Hooks Notifier.lnk");

            if (File.Exists(shortcutPath))
            {
                Console.Error.WriteLine("hooks-notifier: AUMID shortcut already exists");
                return 0;
            }

            var shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = appPath;
            shortcut.Arguments = "--tray";
            shortcut.WorkingDirectory = Path.GetDirectoryName(appPath);
            shortcut.Description = "Claude Code Hooks Notifier — system tray";
            shortcut.Save();

            Console.Error.WriteLine("hooks-notifier: AUMID registration complete (shortcut -> --tray)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"hooks-notifier: registration failed: {ex.Message}");
            return 1;
        }
    }

    // ── Permission dialog (WinForms) ───────────────────────────────────
    private static PermissionDecision ShowPermissionDialog(string toolName, HookData data)
    {
        var decision = new PermissionDecision();

        using var form = new Form
        {
            Text = "Claude Code — Permission Request",
            Size = new Size(520, 330),
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
            Text = "Claude needs authorization",
            Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(20, 16),
            Size = new Size(470, 26)
        });

        form.Controls.Add(new Label
        {
            Text = $"Tool:  {toolName}",
            Font = new Font("Consolas", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(67, 97, 238),
            Location = new Point(20, 50),
            Size = new Size(470, 18)
        });

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

        form.Controls.Add(new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9),
            Location = new Point(20, 78),
            Size = new Size(470, 140),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Text = detailLines.Count > 0 ? string.Join("\r\n", detailLines) : "(no details)",
            TabStop = false
        });

        var bar = new Panel
        {
            Location = new Point(0, 236),
            Size = new Size(520, 50),
            BackColor = Color.FromArgb(241, 243, 245),
            BorderStyle = BorderStyle.FixedSingle
        };
        form.Controls.Add(bar);

        var btnDeny = new Button
        {
            Text = "Deny", Location = new Point(410, 12), Size = new Size(85, 28),
            FlatStyle = FlatStyle.Flat, BackColor = Color.White,
            ForeColor = Color.FromArgb(220, 53, 69),
            DialogResult = DialogResult.No, TabIndex = 2
        };
        btnDeny.FlatAppearance.BorderColor = Color.FromArgb(220, 53, 69);
        bar.Controls.Add(btnDeny);

        var btnAllow = new Button
        {
            Text = "Allow", Location = new Point(310, 12), Size = new Size(85, 28),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(67, 97, 238),
            ForeColor = Color.White, DialogResult = DialogResult.Yes, TabIndex = 0
        };
        btnAllow.FlatAppearance.BorderColor = Color.FromArgb(67, 97, 238);
        bar.Controls.Add(btnAllow);

        form.AcceptButton = btnAllow;
        form.CancelButton = btnDeny;

        if (form.ShowDialog() == DialogResult.Yes)
            decision.Behavior = "allow";

        return decision;
    }
}
