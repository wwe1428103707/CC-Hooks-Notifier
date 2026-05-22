namespace HooksNotifier;

/// <summary>Modern dashboard & configuration window inspired by shadcn/ui design.</summary>
internal partial class MainWindow : Form
{
    private readonly TabControl _tabControl;
    private readonly Label _statusBar;
    private readonly Panel _headerBar;

    // ── Modern color palette ─────────────────────────────────────────
    private static readonly Color BgPage = Color.FromArgb(245, 247, 250);
    private static readonly Color BgCard = Color.White;
    private static readonly Color BgHeader = Color.FromArgb(33, 37, 41);
    private static readonly Color TextPrimary = Color.FromArgb(33, 37, 41);
    private static readonly Color TextSecondary = Color.FromArgb(108, 117, 125);
    private static readonly Color AccentBlue = Color.FromArgb(67, 97, 238);
    private static readonly Color AccentGreen = Color.FromArgb(47, 158, 68);
    private static readonly Color AccentRed = Color.FromArgb(220, 53, 69);
    private static readonly Color AccentOrange = Color.FromArgb(253, 126, 20);
    private static readonly Color BorderLight = Color.FromArgb(222, 226, 230);

    public MainWindow()
    {
        Text = I18n.Get("window.title");
        Size = new Size(960, 640);
        MinimumSize = new Size(860, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgPage;
        Font = new Font("Segoe UI", 9);

        // ── Header bar ───────────────────────────────────────────────
        _headerBar = new Panel
        {
            Height = 52,
            Dock = DockStyle.Top,
            BackColor = BgHeader
        };

        var headerLabel = new Label
        {
            Text = "  ⚡  Claude Code Hooks Notifier",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Location = new Point(16, 12),
            Size = new Size(400, 30),
            AutoSize = false
        };
        _headerBar.Controls.Add(headerLabel);

        var headerSub = new Label
        {
            Text = $"v1.4.0",
            ForeColor = Color.FromArgb(160, 165, 175),
            Font = new Font("Segoe UI", 9),
            Location = new Point(340, 17),
            Size = new Size(80, 20),
            AutoSize = false
        };
        _headerBar.Controls.Add(headerSub);

        // ── Tab control ──────────────────────────────────────────────
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6),
            Font = new Font("Segoe UI", 9)
        };

        _tabControl.TabPages.Add(BuildDashboardTab());
        _tabControl.TabPages.Add(BuildEventLogTab());
        _tabControl.TabPages.Add(BuildSettingsTab());
        _tabControl.TabPages.Add(BuildAboutTab());

        // ── Status bar ───────────────────────────────────────────────
        _statusBar = new Label
        {
            Height = 32,
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(241, 243, 245),
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 8),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Text = $"    {I18n.Get("dashboard.service_running")}  |  {I18n.Get("settings.language")}: {I18n.CurrentLanguage.ToUpper()}  |  v1.4.0"
        };

        Controls.Add(_tabControl);
        Controls.Add(_headerBar);
        Controls.Add(_statusBar);
    }

    /// <summary>Push a new event entry to the dashboard and event log.</summary>
    public void PushEvent(EventEntry entry)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            AddEventToLog(entry);
            RefreshDashboard();
        });
    }

    // ── Card helper ─────────────────────────────────────────────────
    protected static Panel CreateCard(int x, int y, int w, int h)
    {
        return new Panel
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = BgCard,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    protected static Label CardTitle(string text, int x, int y, int w)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = TextSecondary,
            Location = new Point(x, y),
            Size = new Size(w, 18),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    protected static Label CardValue(string text, int x, int y, int w, Color? color = null)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = color ?? TextPrimary,
            Location = new Point(x, y),
            Size = new Size(w, 36),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private void UpdateStatusBar()
    {
        _statusBar.Text = $"    {I18n.Get("dashboard.service_running")}  |  {I18n.Get("settings.language")}: {I18n.CurrentLanguage.ToUpper()}  |  v1.4.0";
    }
}
