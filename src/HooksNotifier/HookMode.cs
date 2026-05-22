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

        // Try IPC first: send to tray process if running
        try
        {
            var ipcResult = IpcService.SendNotification(data);
            if (ipcResult) return 0; // Tray handled it (with blink)
        }
        catch
        {
            // IPC failed — fall through to direct handling
        }

        // Fallback: handle directly (no tray process)
        return HandleDirect(data);
    }

    /// <summary>Handle event directly (no tray process available).</summary>
    private static int HandleDirect(HookData data)
    {
        return data.HookEventName switch
        {
            "PermissionRequest" => HandlePermissionRequest(data),
            "Notification" => HandleNotification(data),
            _ => HandleDefault(data)
        };
    }

    // ── Notification event ─────────────────────────────────────────────
    private static int HandleNotification(HookData data)
    {
        var (title, body) = FormatNotification(data);
        ToastService.Show(title, body);
        return 0;
    }

    public static (string title, string body) FormatNotification(HookData data)
    {
        var eventType = data.HookEventType ?? "";
        var subType = data.HookEventSubtype ?? "";

        var title = "Claude Code";
        var body = eventType switch
        {
            "idle_prompt" => "Task complete — ready for your input",
            "task_start" when !string.IsNullOrEmpty(subType) => $"Task started: {subType}",
            "task_start" => "Task started",
            "task_end" when !string.IsNullOrEmpty(subType) => $"Task completed: {subType}",
            "task_end" => "Task completed",
            _ => $"Event: {eventType} {subType}".TrimEnd()
        };
        return (title, body);
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
