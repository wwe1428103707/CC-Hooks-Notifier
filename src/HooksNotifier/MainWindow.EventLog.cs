namespace HooksNotifier;

internal partial class MainWindow
{
    private ListView _logListView = null!;

    private TabPage BuildEventLogTab()
    {
        var page = new TabPage(I18n.Get("tab.event_log")) { BackColor = BgPage };

        var header = new Label
        {
            Text = I18n.Get("tab.event_log"),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, 14),
            Size = new Size(300, 22)
        };
        page.Controls.Add(header);

        _logListView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Location = new Point(16, 44),
            Size = new Size(904, 502),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Consolas", 9),
            BackColor = BgCard,
            BorderStyle = BorderStyle.FixedSingle
        };

        _logListView.Columns.Add(I18n.Get("event_log.time"), 80);
        _logListView.Columns.Add(I18n.Get("event_log.level"), 70);
        _logListView.Columns.Add(I18n.Get("event_log.event"), 130);
        _logListView.Columns.Add(I18n.Get("event_log.content"), 604);

        foreach (var entry in EventHistory.Entries)
            AddEventToLog(entry);

        // Bottom bar with clear button
        var bottomBar = new Panel
        {
            Location = new Point(0, 528),
            Size = new Size(944, 40),
            BackColor = Color.FromArgb(241, 243, 245),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        var clearBtn = new Button
        {
            Text = I18n.Get("event_log.clear"),
            Location = new Point(16, 8),
            Size = new Size(90, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            FlatAppearance = { BorderColor = BorderLight }
        };
        clearBtn.Click += (_, _) =>
        {
            EventHistory.Clear();
            _logListView.Items.Clear();
            RefreshDashboard();
        };
        bottomBar.Controls.Add(clearBtn);

        // Count label
        var countLabel = new Label
        {
            Text = $"0 {I18n.Get("dashboard.total", "")}",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 9),
            Location = new Point(120, 11),
            Size = new Size(200, 20),
            AutoSize = false
        };
        void updateCount() => countLabel.Text = $"{_logListView.Items.Count} {I18n.Get("dashboard.total", _logListView.Items.Count.ToString())}";
        updateCount();

        // Hook into clear to also update count
        clearBtn.Click += (_, _) => updateCount();
        bottomBar.Controls.Add(countLabel);

        page.Controls.Add(_logListView);
        page.Controls.Add(bottomBar);
        return page;
    }

    private void AddEventToLog(EventEntry entry)
    {
        if (_logListView == null || _logListView.IsDisposed) return;

        var item = new ListViewItem(new[]
        {
            entry.Timestamp.ToString("HH:mm:ss"),
            entry.Level,
            entry.EventName,
            entry.Summary
        });

        item.ForeColor = entry.Level switch
        {
            "P0" => AccentRed,
            "P0.5" => AccentOrange,
            "Toast" => AccentBlue,
            _ => TextSecondary
        };

        _logListView.Items.Add(item);
        _logListView.EnsureVisible(_logListView.Items.Count - 1);
    }
}
