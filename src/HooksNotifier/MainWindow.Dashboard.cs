namespace HooksNotifier;

internal partial class MainWindow
{
    private Label _dashNotifTotal = null!;
    private Label _dashSubagentTotal = null!;
    private Label _dashTaskTotal = null!;
    private Label _dashSessionLabel = null!;
    private Label _dashRecentList = null!;
    private Label _dashP0Total = null!;
    private Label _dashToastTotal = null!;

    private TabPage BuildDashboardTab()
    {
        var page = new TabPage(I18n.Get("tab.dashboard"));
        var bg = Color.FromArgb(248, 249, 250);
        var cardBg = Color.White;
        var accent = Color.FromArgb(67, 97, 238);
        var gray = Color.FromArgb(108, 117, 125);

        // Service status
        var statusLabel = new Label
        {
            Text = $"🟢  {I18n.Get("dashboard.service_running")}",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(20, 16),
            Size = new Size(500, 28)
        };
        page.Controls.Add(statusLabel);

        // Card helper
        Label MakeCard(int x, int y, string title, ref Label valueLabel)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(200, 80),
                BackColor = cardBg,
                BorderStyle = BorderStyle.FixedSingle
            };
            var titleLbl = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = gray,
                Location = new Point(12, 8),
                Size = new Size(176, 18)
            };
            panel.Controls.Add(titleLbl);
            valueLabel = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new Point(12, 32),
                Size = new Size(176, 36),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(valueLabel);
            page.Controls.Add(panel);
            return valueLabel;
        }

        MakeCard(20, 56, I18n.Get("dashboard.notifications"), ref _dashNotifTotal);
        MakeCard(236, 56, "P0 Blinks", ref _dashP0Total);
        MakeCard(452, 56, "Toasts", ref _dashToastTotal);
        MakeCard(20, 150, I18n.Get("dashboard.subagents"), ref _dashSubagentTotal);
        MakeCard(236, 150, I18n.Get("dashboard.tasks"), ref _dashTaskTotal);

        // Session
        _dashSessionLabel = new Label
        {
            Text = $"🟢  {I18n.Get("dashboard.service_running")}",
            Font = new Font("Segoe UI", 9),
            ForeColor = gray,
            Location = new Point(452, 150),
            Size = new Size(200, 80),
            BackColor = cardBg,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 0, 0),
            TextAlign = ContentAlignment.TopLeft
        };
        page.Controls.Add(_dashSessionLabel);

        // Recent events
        page.Controls.Add(new Label
        {
            Text = I18n.Get("dashboard.recent_events"),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(20, 248),
            Size = new Size(500, 20)
        });

        _dashRecentList = new Label
        {
            Font = new Font("Consolas", 9),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(20, 274),
            Size = new Size(840, 220),
            BackColor = cardBg,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(8, 6, 0, 0)
        };
        page.Controls.Add(_dashRecentList);

        RefreshDashboard();
        return page;
    }

    private void RefreshDashboard()
    {
        var (total, p0, p05, toast, stateful) = EventHistory.Counts;
        _dashNotifTotal.Text = total.ToString();
        _dashP0Total.Text = p0.ToString();
        _dashToastTotal.Text = toast.ToString();
        _dashSubagentTotal.Text = TrayMode.SubagentCount.ToString();
        _dashTaskTotal.Text = TrayMode.TaskCount.ToString();

        _dashSessionLabel.Text = $"{I18n.Get("dashboard.service_running")}\n" +
            $"{I18n.Get("dashboard.total", total.ToString())}";

        var recent = EventHistory.GetRecent(5);
        if (recent.Length == 0)
        {
            _dashRecentList.Text = "  (no events yet)";
        }
        else
        {
            var lines = recent.Select(e =>
                $"  [{e.Timestamp:HH:mm:ss}]  {e.Level,-6}  {e.EventName,-18}  {e.Summary}");
            _dashRecentList.Text = string.Join("\n", lines);
        }
    }
}
