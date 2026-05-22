namespace HooksNotifier;

internal partial class MainWindow
{
    private Label _dashNotifValue = null!;
    private Label _dashP0Value = null!;
    private Label _dashToastValue = null!;
    private Label _dashSubValue = null!;
    private Label _dashTaskValue = null!;
    private Label _dashSessValue = null!;
    private Label _dashRecentList = null!;

    private TabPage BuildDashboardTab()
    {
        var page = new TabPage(I18n.Get("tab.dashboard")) { BackColor = BgPage };
        int x = 20, y = 20;
        const int cardW = 186;

        // ── Stat cards ──────────────────────────────────────────────
        AddStatCard(page, ref x, y, I18n.Get("dashboard.notifications"), ref _dashNotifValue, AccentBlue);
        AddStatCard(page, ref x, y, "P0 Blinks", ref _dashP0Value, AccentRed);
        AddStatCard(page, ref x, y, "Toasts", ref _dashToastValue, AccentBlue);

        x = 20; y += 108;
        AddStatCard(page, ref x, y, I18n.Get("dashboard.subagents"), ref _dashSubValue, AccentGreen);
        AddStatCard(page, ref x, y, I18n.Get("dashboard.tasks"), ref _dashTaskValue, AccentGreen);

        // Session card (wider, shows status)
        var sessCard = CreateCard(x, y, cardW, 88);
        sessCard.Controls.Add(CardTitle(I18n.Get("dashboard.service_running"), 14, 8, cardW - 20));
        _dashSessValue = new Label
        {
            Text = "🟢  Running",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = AccentGreen,
            Location = new Point(14, 30),
            Size = new Size(cardW - 20, 48),
            TextAlign = ContentAlignment.MiddleLeft
        };
        sessCard.Controls.Add(_dashSessValue);
        page.Controls.Add(sessCard);

        // ── Recent events section ────────────────────────────────────
        y += 108;
        var sectionTitle = new Label
        {
            Text = I18n.Get("dashboard.recent_events"),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(20, y),
            Size = new Size(400, 22)
        };
        page.Controls.Add(sectionTitle);
        y += 30;

        _dashRecentList = new Label
        {
            Font = new Font("Consolas", 9),
            ForeColor = TextPrimary,
            Location = new Point(20, y),
            Size = new Size(888, 340),
            BackColor = BgCard,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(14, 10, 0, 0)
        };
        page.Controls.Add(_dashRecentList);

        RefreshDashboard();
        return page;
    }

    private void AddStatCard(TabPage page, ref int x, int y, string title, ref Label valueRef, Color accent)
    {
        var card = CreateCard(x, y, 186, 88);
        // Accent top border
        var accentLine = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(186, 3),
            BackColor = accent
        };
        card.Controls.Add(accentLine);
        card.Controls.Add(CardTitle(title, 14, 14, 160));
        valueRef = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(14, 36),
            Size = new Size(160, 38),
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(valueRef);
        page.Controls.Add(card);
        x += 202;
    }

    private void RefreshDashboard()
    {
        var (total, p0, p05, toast, stateful) = EventHistory.Counts;
        SetText(_dashNotifValue, total.ToString());
        SetText(_dashP0Value, p0.ToString());
        SetText(_dashToastValue, toast.ToString());
        SetText(_dashSubValue, TrayMode.SubagentCount.ToString());
        SetText(_dashTaskValue, TrayMode.TaskCount.ToString());

        var recent = EventHistory.GetRecent(5);
        if (recent.Length == 0)
        {
            SetText(_dashRecentList, "  (no events yet)");
        }
        else
        {
            var lines = recent.Select(e =>
            {
                var icon = e.Level switch
                {
                    "P0" => "🔴",
                    "P0.5" => "🟠",
                    "Toast" => "🔵",
                    _ => "⚪"
                };
                return $"  {icon}  [{e.Timestamp:HH:mm:ss}]  {e.EventName,-20}  {e.Summary}";
            });
            SetText(_dashRecentList, string.Join("\n", lines));
        }
    }

    private static void SetText(Label? lbl, string text)
    {
        if (lbl != null && !lbl.IsDisposed) lbl.Text = text;
    }
}
