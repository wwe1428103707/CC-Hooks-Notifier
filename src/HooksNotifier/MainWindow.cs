namespace HooksNotifier;

/// <summary>
/// Main configuration and dashboard window.
/// Opened by double-clicking the tray icon.
/// </summary>
internal partial class MainWindow : Form
{
    private readonly TabControl _tabControl;
    private readonly Label _statusBar;

    public MainWindow()
    {
        Text = I18n.Get("window.title");
        Size = new Size(900, 600);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 249, 250);
        Font = new Font("Segoe UI", 9);

        _tabControl = new TabControl
        {
            Location = new Point(0, 0),
            Size = new Size(900, 560),
            Dock = DockStyle.Fill
        };

        _tabControl.TabPages.Add(BuildDashboardTab());
        _tabControl.TabPages.Add(BuildEventLogTab());
        _tabControl.TabPages.Add(BuildSettingsTab());
        _tabControl.TabPages.Add(BuildAboutTab());

        // Status bar
        _statusBar = new Label
        {
            Location = new Point(0, 560),
            Size = new Size(900, 40),
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(241, 243, 245),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Text = $"  {I18n.Get("dashboard.service_running")}  |  {I18n.Get("settings.language")}: {I18n.CurrentLanguage.ToUpper()}  |  v1.4.0"
        };

        Controls.Add(_tabControl);
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
}
