namespace HooksNotifier;

internal partial class MainWindow
{
    private TabPage BuildAboutTab()
    {
        var page = new TabPage(I18n.Get("tab.about")) { BackColor = BgPage };

        var card = CreateCard(20, 20, 880, 540);
        int y = 24;

        // App icon + name
        card.Controls.Add(new Label
        {
            Text = "⚡",
            Font = new Font("Segoe UI", 36),
            Location = new Point(24, y),
            Size = new Size(60, 50),
            TextAlign = ContentAlignment.MiddleCenter
        });

        card.Controls.Add(new Label
        {
            Text = I18n.Get("about.title"),
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(92, y),
            Size = new Size(500, 32)
        });

        y += 48;

        card.Controls.Add(new Label
        {
            Text = I18n.Get("about.version", "1.4.0"),
            Font = new Font("Segoe UI", 11),
            ForeColor = TextSecondary,
            Location = new Point(24, y),
            Size = new Size(500, 24)
        });
        y += 32;

        card.Controls.Add(new Label
        {
            Text = I18n.Get("about.tech_stack"),
            Font = new Font("Segoe UI", 9),
            ForeColor = TextSecondary,
            Location = new Point(24, y),
            Size = new Size(500, 20)
        });
        y += 28;

        card.Controls.Add(new Label
        {
            Text = "Windows system tray notification service for Claude Code hooks.\nMonitors PermissionRequest, Notification, StopFailure, and more.\nBell icon with P0/P0.5 blinking, P1/P2 stateful tracking.",
            Font = new Font("Segoe UI", 9),
            ForeColor = TextPrimary,
            Location = new Point(24, y),
            Size = new Size(600, 56)
        });

        y += 72;

        // Coverage table
        card.Controls.Add(new Label
        {
            Text = "Hook Event Coverage",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(24, y),
            Size = new Size(400, 22)
        });
        y += 30;

        var coverage = new[]
        {
            ("P0 🔔  ", "Long blink (10s)", "idle_prompt, permission_prompt, StopFailure (error)"),
            ("P0.5 🔔", "Short blink (5s)", "Stop, TaskCompleted, SessionEnd"),
            ("P1 📢  ", "Toast", "auth_success, elicitation, PermissionDenied, PostToolUse, etc."),
            ("P2 🟢  ", "Stateful", "SubagentStart, TaskCreated, PreCompact")
        };

        // Table header
        var headerFont = new Font("Segoe UI", 8, FontStyle.Bold);
        var headerColor = TextSecondary;
        card.Controls.Add(new Label { Text = "Level", Font = headerFont, ForeColor = headerColor, Location = new Point(24, y), Size = new Size(70, 18) });
        card.Controls.Add(new Label { Text = "Type", Font = headerFont, ForeColor = headerColor, Location = new Point(100, y), Size = new Size(100, 18) });
        card.Controls.Add(new Label { Text = "Events", Font = headerFont, ForeColor = headerColor, Location = new Point(206, y), Size = new Size(400, 18) });
        y += 22;

        // Separator
        card.Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(24, y), Size = new Size(780, 2), AutoSize = false });
        y += 10;

        foreach (var (level, type, events) in coverage)
        {
            card.Controls.Add(new Label { Text = level, Font = new Font("Consolas", 9), ForeColor = TextPrimary, Location = new Point(24, y), Size = new Size(70, 20) });
            card.Controls.Add(new Label { Text = type, Font = new Font("Segoe UI", 9), ForeColor = TextSecondary, Location = new Point(100, y), Size = new Size(100, 20) });
            card.Controls.Add(new Label { Text = events, Font = new Font("Consolas", 8), ForeColor = TextSecondary, Location = new Point(206, y), Size = new Size(600, 20) });
            y += 22;
        }

        page.Controls.Add(card);
        return page;
    }
}
