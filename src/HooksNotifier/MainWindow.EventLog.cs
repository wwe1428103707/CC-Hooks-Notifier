namespace HooksNotifier;

internal partial class MainWindow
{
    private ListView _logListView = null!;

    private TabPage BuildEventLogTab()
    {
        var page = new TabPage(I18n.Get("tab.event_log"));

        _logListView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Location = new Point(12, 12),
            Size = new Size(860, 500),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Consolas", 9)
        };

        _logListView.Columns.Add(I18n.Get("event_log.time"), 80);
        _logListView.Columns.Add(I18n.Get("event_log.level"), 70);
        _logListView.Columns.Add(I18n.Get("event_log.event"), 120);
        _logListView.Columns.Add(I18n.Get("event_log.content"), 560);

        // Populate existing history
        foreach (var entry in EventHistory.Entries)
            AddEventToLog(entry);

        // Clear button
        var clearBtn = new Button
        {
            Text = I18n.Get("event_log.clear"),
            Location = new Point(12, 522),
            Size = new Size(100, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        clearBtn.Click += (_, _) =>
        {
            EventHistory.Clear();
            _logListView.Items.Clear();
            RefreshDashboard();
        };

        page.Controls.Add(_logListView);
        page.Controls.Add(clearBtn);
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

        // Color coding
        item.ForeColor = entry.Level switch
        {
            "P0" => Color.FromArgb(220, 53, 69),     // red
            "P0.5" => Color.FromArgb(253, 126, 20),  // orange
            "Toast" => Color.FromArgb(13, 110, 253), // blue
            _ => Color.FromArgb(73, 80, 87)          // gray
        };

        _logListView.Items.Add(item);
        _logListView.EnsureVisible(_logListView.Items.Count - 1);
    }
}
