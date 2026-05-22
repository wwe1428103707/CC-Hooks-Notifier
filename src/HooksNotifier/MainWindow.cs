using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace HooksNotifier;

/// <summary>WebView2 host for the React/shadcn UI.</summary>
internal partial class MainWindow : Form
{
    private readonly WebView2 _webView;
    private bool _loaded;

    public MainWindow()
    {
        Text = "Claude Code Hooks Notifier";
        Size = new Size(1200, 800);
        MinimumSize = new Size(960, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);

        _webView = new WebView2 { Dock = DockStyle.Fill };
        _webView.CoreWebView2InitializationCompleted += OnWebViewReady;
        Controls.Add(_webView);

        _webView.EnsureCoreWebView2Async();
    }

    private void OnWebViewReady(object? sender, EventArgs e)
    {
        if (_webView.CoreWebView2 == null) return;

        // Serve webui from bin/webui/
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
            // Fallback: dev server
            _webView.CoreWebView2.Navigate("http://localhost:5173");
        }

        // Disable right-click and dev tools in release
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

        // Listen for JS messages
        _webView.CoreWebView2.WebMessageReceived += OnJsMessage;

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
                summary = entry.Summary
            }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _webView.CoreWebView2?.PostWebMessageAsJson(json);
    }

    public void PushState(string type, object payload)
    {
        if (IsDisposed || !_loaded) return;
        var json = JsonSerializer.Serialize(new { type, payload },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _webView.CoreWebView2?.PostWebMessageAsJson(json);
    }

    private object GetCurrentState()
    {
        var (total, p0, p05, toast, stateful) = EventHistory.Counts;
        return new
        {
            counts = new { total, p0, p05, toast, stateful },
            subagentCount = TrayMode.SubagentCount,
            taskCount = TrayMode.TaskCount,
            recentEvents = EventHistory.GetRecent(5).Select(e => new
            {
                timestamp = e.Timestamp.ToString("HH:mm:ss"),
                level = e.Level,
                eventName = e.EventName,
                summary = e.Summary
            }),
            allEvents = EventHistory.Entries.Reverse().Take(100).Select(e => new
            {
                timestamp = e.Timestamp.ToString("HH:mm:ss"),
                level = e.Level,
                eventName = e.EventName,
                summary = e.Summary
            }),
            language = I18n.CurrentLanguage
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
            }
        }
        catch { /* ignore malformed messages */ }
    }
}
