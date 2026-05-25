using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace HooksNotifier;

/// <summary>WebView2 host for the React/shadcn UI. Pre-warmed on tray startup for instant open.</summary>
internal partial class MainWindow : Form
{
    private readonly WebView2 _webView;
    private readonly Label _loadingLabel;
    private bool _loaded;
    private bool _pendingTrayOpen;
    private static MainWindow? _preWarmed;

    /// <summary>Signal that this window was opened via tray icon click (sets defaultFilter to "unread").</summary>
    public void MarkTrayOpen() => _pendingTrayOpen = true;

    /// <summary>Pre-warm WebView2 at tray startup so first open skips cold start.</summary>
    public static void PreWarm()
    {
        if (_preWarmed != null) return;
        _preWarmed = new MainWindow();
        _preWarmed.Show();  // triggers WebView2 environment creation
        _preWarmed.Hide();  // keep initialized, ready to show
    }

    /// <summary>Return pre-warmed instance or create a new one if needed.</summary>
    public static MainWindow GetOrCreate()
    {
        if (_preWarmed != null)
        {
            var w = _preWarmed;
            _preWarmed = null;
            return w;
        }
        return new MainWindow();
    }

    public MainWindow()
    {
        Text = "Claude Code Hooks Notifier";
        Size = new Size(1200, 800);
        MinimumSize = new Size(960, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(22, 27, 34); // dark — avoids white flash
        var iconDir = Path.GetDirectoryName(Environment.ProcessPath);
        var iconFile = iconDir != null ? Path.Combine(iconDir, "icon.ico") : null;
        if (iconFile != null && File.Exists(iconFile))
            Icon = new Icon(iconFile);

        // Loading overlay while WebView2 initializes
        _loadingLabel = new Label
        {
            Text = "Claude Code Hooks Notifier",
            Font = new Font("Microsoft YaHei UI", 14, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 190, 200),
            BackColor = Color.FromArgb(22, 27, 34),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Visible = true
        };
        Controls.Add(_loadingLabel);

        _webView = new WebView2 { Dock = DockStyle.Fill };
        _webView.CoreWebView2InitializationCompleted += OnWebViewReady;
        Controls.Add(_webView);
        _loadingLabel.BringToFront();

        _webView.EnsureCoreWebView2Async();
    }

    private void OnWebViewReady(object? sender, EventArgs e)
    {
        if (_webView.CoreWebView2 == null) return;

        var webuiPath = Path.Combine(AppContext.BaseDirectory, "webui");
        if (Directory.Exists(webuiPath))
        {
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.local", webuiPath,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.DenyCors);
            _webView.CoreWebView2.Navigate("https://app.local/index.html");
        }
        else
        {
            _webView.CoreWebView2.Navigate("http://localhost:5173");
        }

        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.WebMessageReceived += OnJsMessage;

        // Hide loading overlay once page loads
        _webView.CoreWebView2.NavigationCompleted += (_, _) =>
        {
            if (_loadingLabel.IsHandleCreated)
                _loadingLabel.BeginInvoke(() => _loadingLabel.Visible = false);
        };

        _loaded = true;
        PushState("state_sync", GetCurrentState());
    }

    // ── C# → JS ─────────────────────────────────────────────────────
    public void PushEvent(EventEntry entry)
    {
        if (IsDisposed || !_loaded) return;
        var json = JsonSerializer.Serialize(new
        {
            type = "event_push",
            payload = new
            {
                timestamp = entry.Timestamp.ToString("HH:mm:ss"),
                level = entry.Level,
                eventName = entry.EventName,
                summary = entry.Summary,
                detail = entry.Detail ?? "",
                isRead = entry.IsRead,
                unreadCount = EventHistory.UnreadCount
            }
        }, SharedJsonOpts());
        _webView.CoreWebView2?.PostWebMessageAsJson(json);
    }

    public void PushState(string type, object payload)
    {
        if (IsDisposed || !_loaded) return;
        var json = JsonSerializer.Serialize(new { type, payload }, SharedJsonOpts());
        _webView.CoreWebView2?.PostWebMessageAsJson(json);
    }

    private object GetCurrentState()
    {
        var (total, p0, p05, toast, stateful) = EventHistory.Counts;
        var entries = EventHistory.Entries;
        var df = _pendingTrayOpen ? "unread" : "all";
        _pendingTrayOpen = false;
        return new
        {
            counts = new { total, p0, p05, toast, stateful },
            unreadCount = EventHistory.UnreadCount,
            subagentCount = TrayMode.SubagentCount,
            taskCount = TrayMode.TaskCount,
            recentEvents = EventHistory.GetRecent(5).Select((e, i) => new
            {
                timestamp = e.Timestamp.ToString("HH:mm:ss"),
                level = e.Level,
                eventName = e.EventName,
                summary = e.Summary,
                detail = e.Detail ?? "",
                isRead = e.IsRead
            }),
            allEvents = entries
                .Select((e, i) => new { e, i })
                .Reverse()
                .Take(100)
                .Select(x => new
                {
                    timestamp = x.e.Timestamp.ToString("HH:mm:ss"),
                    level = x.e.Level,
                    eventName = x.e.EventName,
                    summary = x.e.Summary,
                    detail = x.e.Detail ?? "",
                    isRead = x.e.IsRead,
                    _idx = x.i
                }),
            maxEntries = EventHistory.MaxEntries,
            language = I18n.CurrentLanguage,
            hookConfig = HookConfig.GetAllStates(),
            defaultFilter = df
        };
    }

    // ── JS → C# ─────────────────────────────────────────────────────
    private void OnJsMessage(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var doc = JsonDocument.Parse(e.TryGetWebMessageAsString());
            var type = doc.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "get_state":
                    PushState("state_sync", GetCurrentState());
                    break;
                case "set_lang":
                    var lang = doc.RootElement.GetProperty("payload").GetString();
                    if (lang != null)
                    {
                        I18n.SetLanguage(lang);
                        PushState("lang_changed", lang);
                    }
                    break;

                case "update_hook_path":
                    Task.Run(async () =>
                    {
                        try
                        {
                            var exe = Environment.ProcessPath;
                            if (string.IsNullOrEmpty(exe)) return;
                            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--configure-hooks")
                            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true });
                            if (proc == null) return;
                            var output = proc.StandardError.ReadToEnd();
                            proc.WaitForExit(10000);
                            var success = proc.ExitCode == 0;
                            BeginInvoke(() => PushState("configure_hooks_result", new
                            {
                                success,
                                message = success
                                    ? (output?.Replace("ERROR:", "")?.Trim() ?? "Hook path updated")
                                    : (output?.Replace("ERROR:", "")?.Trim() ?? "Update failed")
                            }));
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"configure-hooks failed: {ex.Message}");
                        }
                    });
                    break;

                case "get_hook_config":
                    PushState("hook_config", HookConfig.GetAllStates());
                    break;

                case "set_hook_config":
                    if (doc.RootElement.TryGetProperty("payload", out var cfgPayload))
                    {
                        var key = cfgPayload.GetProperty("key").GetString();
                        var enabled = cfgPayload.GetProperty("enabled").GetBoolean();
                        if (key != null)
                        {
                            if (enabled) HookConfig.Enable(key);
                            else HookConfig.Disable(key);
                            // Refresh config UI
                            PushState("hook_config", HookConfig.GetAllStates());
                        }
                    }
                    break;

                case "mark_all_read":
                    EventHistory.MarkAllRead();
                    TrayMode.UpdateTooltip();
                    PushState("state_sync", GetCurrentState());
                    break;

                case "mark_read":
                    if (doc.RootElement.TryGetProperty("payload", out var mrPayload))
                    {
                        var idx = mrPayload.GetProperty("index").GetInt32();
                        EventHistory.MarkRead(idx);
                        TrayMode.UpdateTooltip();
                    }
                    PushState("state_sync", GetCurrentState());
                    break;

                case "set_max_entries":
                    if (doc.RootElement.TryGetProperty("payload", out var mePayload))
                    {
                        var max = mePayload.GetProperty("value").GetInt32();
                        EventHistory.MaxEntries = max;
                    }
                    PushState("state_sync", GetCurrentState());
                    break;

                case "clear_history":
                    EventHistory.Clear();
                    PushState("state_sync", GetCurrentState());
                    break;

                case "open_settings":
                    var settingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".claude", "settings.json");
                    if (File.Exists(settingsPath))
                    {
                        using var _ = System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo(settingsPath)
                            { UseShellExecute = true });
                    }
                    break;
            }
        }
        catch { /* ignore malformed messages */ }
    }

    private static JsonSerializerOptions SharedJsonOpts() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
