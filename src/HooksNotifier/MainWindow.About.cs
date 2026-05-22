namespace HooksNotifier;

internal partial class MainWindow
{
    private TabPage BuildAboutTab()
    {
        var page = new TabPage(I18n.Get("tab.about"));
        var gray = Color.FromArgb(108, 117, 125);

        page.Controls.Add(new Label
        {
            Text = I18n.Get("about.title"),
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(24, 24),
            Size = new Size(500, 36)
        });

        page.Controls.Add(new Label
        {
            Text = I18n.Get("about.version", "1.4.0"),
            Font = new Font("Segoe UI", 11),
            ForeColor = gray,
            Location = new Point(24, 68),
            Size = new Size(500, 24)
        });

        page.Controls.Add(new Label
        {
            Text = I18n.Get("about.tech_stack"),
            Font = new Font("Segoe UI", 9),
            ForeColor = gray,
            Location = new Point(24, 104),
            Size = new Size(600, 20)
        });

        page.Controls.Add(new Label
        {
            Text = "Windows system tray notification service for Claude Code hooks.\n" +
                   "Monitors PermissionRequest, Notification, StopFailure, and more.",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(24, 140),
            Size = new Size(600, 40)
        });

        // Hook event coverage summary
        page.Controls.Add(new Label
        {
            Text = "Hook Event Coverage:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(24, 200),
            Size = new Size(400, 20)
        });

        var coverage = new[]
        {
            "P0 🔔    idle_prompt, permission_prompt, StopFailure (rate_limit|server_error|auth_failed)",
            "P0.5 🔔  Stop, TaskCompleted, SessionEnd (clear|logout|prompt_input_exit)",
            "P1 📢    auth_success, elicitation_*, PermissionDenied, PostToolUse, PostToolUseFailure, SubagentStop, SessionStart, PostCompact, ConfigChange",
            "P2 🟢    SubagentStart, TaskCreated, PreCompact"
        };

        int y = 228;
        foreach (var line in coverage)
        {
            page.Controls.Add(new Label
            {
                Text = line,
                Font = new Font("Consolas", 8),
                ForeColor = Color.FromArgb(73, 80, 87),
                Location = new Point(24, y),
                Size = new Size(800, 18)
            });
            y += 20;
        }

        return page;
    }
}
