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
        // Enable high-DPI rendering so the dialog is crisp on scaled displays
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

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

        // Send selected options back to Claude Code
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

    /// <summary>Show a modern WinForms dialog with Allow/Deny, option radio buttons, and free-text input.</summary>
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

        var suggestions = new List<JsonElement>();
        if (data.PermissionSuggestions != null)
            foreach (var s in data.PermissionSuggestions)
                suggestions.Add(s);

        var hasSuggestions = suggestions.Count > 0;
        var detailText = detailLines.Count > 0 ? string.Join("\r\n", detailLines) : "(no details)";
        var hasDetails = detailText != "(no details)";

        // ── Layout constants ────────────────────────────────────────
        const int W = 800;
        const int pad = 28;
        var headerH = 64;
        var detailsH = hasDetails ? Math.Min(140, 20 + detailLines.Count * 20) : 0;
        var cardH = 52;
        var optionsGapY = 6;
        var optionsHeaderH = hasSuggestions ? 26 : 0;
        var rememberH = hasSuggestions ? 36 : 0;
        var btnBarH = 64;
        var contentY = headerH + 20;

        // ── Colors ──────────────────────────────────────────────────
        var cBg = Color.FromArgb(250, 251, 252);
        var cHeaderBg = Color.FromArgb(22, 27, 34);
        var cWhite = Color.White;
        var cText = Color.FromArgb(36, 41, 47);
        var cSubText = Color.FromArgb(110, 118, 129);
        var cAccent = Color.FromArgb(31, 111, 235);
        var cAccentHover = Color.FromArgb(24, 91, 196);
        var cDanger = Color.FromArgb(220, 53, 69);
        var cDangerHover = Color.FromArgb(191, 45, 59);
        var cBorder = Color.FromArgb(208, 215, 222);
        var cCardHover = Color.FromArgb(236, 243, 254);
        var cCardSelected = Color.FromArgb(218, 233, 254);
        var cGrayBg = Color.FromArgb(246, 248, 250);

        // ── Form (height finalized after layout) ───────────────────────
        using var form = new Form
        {
            Text = "",
            Size = new Size(W, 600),  // temporary; recalculated below
            StartPosition = FormStartPosition.CenterScreen,
            TopMost = true,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = cBg,
            Padding = new Padding(0),
            Font = new Font("Microsoft YaHei UI", 9)
        };

        // ── Drag-to-move support ────────────────────────────────────
        bool dragging = false; Point dragStart = Point.Empty;
        form.MouseDown += (_, e) => { if (e.Y < headerH) { dragging = true; dragStart = e.Location; } };
        form.MouseMove += (_, e) => { if (dragging) { form.Location = new Point(form.Location.X + e.X - dragStart.X, form.Location.Y + e.Y - dragStart.Y); } };
        form.MouseUp += (_, _) => dragging = false;

        // ── Header bar ──────────────────────────────────────────────
        var headerBar = new Panel
        {
            BackColor = cHeaderBg,
            Location = new Point(0, 0),
            Size = new Size(W, headerH)
        };
        form.Controls.Add(headerBar);

        headerBar.MouseDown += (_, e) => { dragging = true; dragStart = e.Location; };
        headerBar.MouseMove += (_, e) => { if (dragging) { form.Location = new Point(form.Location.X + e.X - dragStart.X, form.Location.Y + e.Y - dragStart.Y); } };
        headerBar.MouseUp += (_, _) => dragging = false;

        headerBar.Controls.Add(new Label
        {
            Text = "Claude Code — Permission Required",
            Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold),
            ForeColor = cWhite,
            Location = new Point(pad, 18),
            Size = new Size(W - 60, 30),
            BackColor = Color.Transparent
        });

        // ── Tool badge ──────────────────────────────────────────────
        var toolBadge = new Panel
        {
            BackColor = Color.FromArgb(40, 50, 62),
            Location = new Point(pad, contentY),
            Size = new Size(W - pad * 2, 42),
        };
        form.Controls.Add(toolBadge);

        toolBadge.Controls.Add(new Label
        {
            Text = $"Tool: {tool}",
            Font = new Font("Consolas", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(88, 166, 255),
            Location = new Point(16, 10),
            Size = new Size(W - 100, 22),
            BackColor = Color.Transparent
        });
        contentY += 52;

        // ── Details section ─────────────────────────────────────────
        if (hasDetails)
        {
            var detailPanel = new Panel
            {
                BackColor = cWhite,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(pad, contentY),
                Size = new Size(W - pad * 2, detailsH)
            };
            form.Controls.Add(detailPanel);

            detailPanel.Controls.Add(new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10),
                Location = new Point(12, 10),
                Size = new Size(detailPanel.Width - 26, detailPanel.Height - 20),
                BackColor = cWhite,
                BorderStyle = BorderStyle.None,
                ForeColor = cText,
                Text = detailText,
                TabStop = false
            });
            contentY += detailsH + 12;
        }

        // ── Options section ─────────────────────────────────────────
        var radioButtons = new List<RadioButton>();
        var textBoxes = new List<TextBox>();
        var rememberCb = (CheckBox?)null;

        if (hasSuggestions)
        {
            var optLabel = new Label
            {
                Text = suggestions.Count == 1 ? "Available option" : "Choose an option",
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
                ForeColor = cSubText,
                Location = new Point(pad, contentY),
                Size = new Size(300, 18),
                BackColor = Color.Transparent
            };
            form.Controls.Add(optLabel);
            contentY += optionsHeaderH;

            for (int i = 0; i < suggestions.Count; i++)
            {
                var desc = DescribeSuggestion(suggestions[i], tool);
                var isFreeInput = IsFreeInputSuggestion(suggestions[i]);
                var rowY = contentY + i * (cardH + optionsGapY);

                var card = new Panel
                {
                    Location = new Point(pad, rowY),
                    Size = new Size(W - pad * 2, cardH),
                    BackColor = i == 0 ? cCardSelected : cGrayBg,
                    Tag = i
                };
                form.Controls.Add(card);

                var rb = new RadioButton
                {
                    Text = isFreeInput ? $"{desc} — " : desc,
                    Location = new Point(18, 16),
                    Size = isFreeInput ? new Size(240, 20) : new Size(card.Width - 40, 20),
                    Font = new Font("Microsoft YaHei UI", 10),
                    ForeColor = cText,
                    BackColor = Color.Transparent,
                    TabIndex = 10 + i,
                    Checked = i == 0,
                    AutoSize = !isFreeInput
                };
                card.Controls.Add(rb);
                radioButtons.Add(rb);

                if (isFreeInput)
                {
                    var tb = new TextBox
                    {
                        Location = new Point(260, 12),
                        Size = new Size(card.Width - 280, 28),
                        Font = new Font("Consolas", 10),
                        BorderStyle = BorderStyle.FixedSingle,
                        BackColor = cWhite,
                        TabIndex = 10 + i + 100,
                        Enabled = rb.Checked,
                        PlaceholderText = "Enter custom command..."
                    };
                    card.Controls.Add(tb);
                    textBoxes.Add(tb);

                    var capturedTb = tb;
                    rb.CheckedChanged += (_, _) => { capturedTb.Enabled = rb.Checked; };
                }
                else
                {
                    textBoxes.Add(null!);
                }

                int capturedI = i;
                rb.CheckedChanged += (_, _) =>
                {
                    if (rb.Checked)
                    {
                        for (int j = 0; j < radioButtons.Count; j++)
                        {
                            if (j != capturedI && radioButtons[j].Checked)
                                radioButtons[j].Checked = false;
                        }
                    }
                    for (int j = 0; j < radioButtons.Count; j++)
                    {
                        var cj = form.Controls.OfType<Panel>().FirstOrDefault(p => p.Tag is int tj && tj == j);
                        if (cj != null)
                            cj.BackColor = radioButtons[j].Checked ? cCardSelected : cGrayBg;
                    }
                };

                card.MouseEnter += (_, _) => { if (!rb.Checked) card.BackColor = cCardHover; };
                card.MouseLeave += (_, _) => { if (!rb.Checked) card.BackColor = cGrayBg; };
                card.Click += (_, _) => { rb.Checked = true; };
            }
            contentY += suggestions.Count * (cardH + optionsGapY) + optionsGapY;

            rememberCb = new CheckBox
            {
                Text = "Always allow — don't ask again for this choice",
                Location = new Point(pad + 4, contentY),
                Size = new Size(450, 24),
                Font = new Font("Microsoft YaHei UI", 9),
                ForeColor = cSubText,
                BackColor = Color.Transparent,
                TabIndex = 200
            };
            form.Controls.Add(rememberCb);
            contentY += rememberH;
        }

        // ── Finalize form height ───────────────────────────────────────
        var formH = contentY + 72 + btnBarH;
        form.Size = new Size(W, formH);

        // ── Bottom bar ──────────────────────────────────────────────
        var btnBar = new Panel
        {
            Location = new Point(0, formH - btnBarH),
            Size = new Size(W, btnBarH),
            BackColor = Color.White
        };
        btnBar.Paint += (_, pe) => { pe.Graphics.DrawLine(new Pen(Color.FromArgb(230, 234, 239)), 0, 0, W, 0); };
        form.Controls.Add(btnBar);

        var btnW = 100;
        var btnH = 38;
        var btnY = (btnBarH - btnH) / 2;
        var btnDeny = CreateModernButton("Deny",
            new Point(W - pad - btnW * 2 - 12, btnY), new Size(btnW, btnH),
            cWhite, cDanger, cDangerHover, Color.FromArgb(220, 53, 69), DialogResult.No);
        btnBar.Controls.Add(btnDeny);

        var btnAllow = CreateModernButton("Allow",
            new Point(W - pad - btnW, btnY), new Size(btnW, btnH),
            cAccent, cWhite, cAccentHover, cAccent, DialogResult.Yes);
        btnBar.Controls.Add(btnAllow);

        form.AcceptButton = btnAllow;
        form.CancelButton = btnDeny;

        var dialogResult = form.ShowDialog();
        var allowed = dialogResult == DialogResult.Yes;

        // ── Collect selected options ───────────────────────────────
        var selectedPerms = new List<object>();
        if (allowed && hasSuggestions)
        {
            for (int i = 0; i < radioButtons.Count; i++)
            {
                if (radioButtons[i].Checked)
                {
                    var suggestion = suggestions[i];
                    if (textBoxes[i] != null && !string.IsNullOrWhiteSpace(textBoxes[i].Text))
                        suggestion = InjectCustomRule(suggestions[i], textBoxes[i].Text.Trim());
                    selectedPerms.Add(suggestion);
                    break;
                }
            }
        }

        return new PermissionDialogOutcome(allowed, selectedPerms);
    }

    /// <summary>Create a modern styled button with rounded corners and hover effect.</summary>
    static Button CreateModernButton(string text, Point loc, Size size,
        Color bg, Color fg, Color hoverBg, Color borderColor, DialogResult result)
    {
        var btn = new Button
        {
            Text = text,
            Location = loc,
            Size = size,
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = fg,
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
            DialogResult = result,
            TabIndex = result == DialogResult.Yes ? 0 : 2
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = borderColor;
        btn.FlatAppearance.MouseOverBackColor = hoverBg;
        btn.FlatAppearance.MouseDownBackColor = hoverBg;
        return btn;
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

    /// <summary>Check if a suggestion has empty ruleContent (allows free-text input).</summary>
    static bool IsFreeInputSuggestion(JsonElement s)
    {
        var type = s.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        if (type != "addRules") return false;
        if (!s.TryGetProperty("rules", out var rules)) return false;
        foreach (var rule in rules.EnumerateArray())
        {
            var rc = rule.TryGetProperty("ruleContent", out var rv) ? rv.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(rc)) return true; // empty = free input
        }
        return false;
    }

    /// <summary>Inject a custom command into an addRules suggestion with empty ruleContent.</summary>
    static JsonElement InjectCustomRule(JsonElement original, string customText)
    {
        var json = original.GetRawText();
        // Replace the last empty ruleContent with the user's custom text
        var patched = json.Replace("\"ruleContent\":\"\"", $"\"ruleContent\":\"{EscapeJson(customText)}\"");
        patched = patched.Replace("\"ruleContent\": \"\"", $"\"ruleContent\": \"{EscapeJson(customText)}\"");
        return JsonDocument.Parse(patched).RootElement;
    }

    static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

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
            var blinkType = GetBlinkType(data);
            var msg = JsonSerializer.Serialize(new
            {
                type = "toast",
                title,
                body,
                detail,
                eventName = data.HookEventName ?? "",
                eventType = data.HookEventType ?? "",
                blinkType
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

    /// <summary>Map hook event to blink type for tray notification.</summary>
    static string GetBlinkType(HookData data)
    {
        return data.HookEventName switch
        {
            "StopFailure" => "long",
            "PermissionRequest" => "long",
            "Notification" => data.HookEventType switch
            {
                "idle_prompt" => "long",
                "permission_prompt" => "long",
                _ => "none"
            },
            "Stop" => "short",
            "TaskCompleted" => "short",
            "SessionEnd" => "short",
            _ => "none"
        };
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
