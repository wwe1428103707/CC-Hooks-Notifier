using System.Diagnostics;
using Microsoft.Win32;

namespace HooksNotifier;

/// <summary>
/// --tray mode: background process with system tray icon, blinking animation,
/// and context menu. Listens for IPC messages from --hook mode.
/// All WinForms/NotifyIcon operations run on the UI thread via SynchronizationContext.
/// </summary>
internal static class TrayMode
{
    private const string MutexName = "ClaudeCodeHooksTray";
    private const int BlinkIntervalMs = 500;
    private const int DefaultBlinkTicks = 20;

    private static NotifyIcon? _trayIcon;
    private static System.Windows.Forms.Timer? _blinkTimer;
    private static int _blinkTick;
    private static int _maxBlinkTicks = DefaultBlinkTicks;
    private static bool _isHighlighted;
    private static CancellationTokenSource? _cts;
    private static SynchronizationContext? _uiContext;

    // ── Stateful counters (updated via IPC) ─────────────────────────
    private static int _subagentCount;
    private static int _taskCount;
    private static string _lastAgentType = "";
    private static string _lastTaskDesc = "";

    /// <summary>Total subagent starts (for dashboard).</summary>
    public static int SubagentCount => _subagentCount;
    /// <summary>Total task creations (for dashboard).</summary>
    public static int TaskCount => _taskCount;

    // ── Dashboard window ──────────────────────────────────────────────
    private static MainWindow? _mainWindow;

    // ── Dynamic menu items (updated on each stateful message) ────────
    private static ToolStripMenuItem? _statusCountItem;
    private static ToolStripMenuItem? _statusSubagentItem;
    private static ToolStripMenuItem? _statusTaskItem;

    public static int Run()
    {
        // Single instance check
        using var mutex = new Mutex(true, MutexName, out var created);
        if (!created)
            return 0; // Another instance is running

        ApplicationConfiguration.Initialize();
        // Ensure WindowsFormsSynchronizationContext is installed for UI thread dispatching
        if (SynchronizationContext.Current == null)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _uiContext = SynchronizationContext.Current;
        _cts = new CancellationTokenSource();

        // Global exception handlers to prevent silent crashes
        Application.ThreadException += (_, e) =>
        {
            Log.Error($"ThreadException: {e.Exception.Message}");
            _uiContext?.Post(_ => ToastService.ShowBalloon("Error",
                $"An unexpected error occurred:\n{e.Exception.Message}"), null);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Error($"UnhandledException: {e.ExceptionObject}");
        };

        // ── Build tray icon ──────────────────────────────────────────
        _trayIcon = new NotifyIcon
        {
            Icon = IconHelper.Normal,
            Text = "Claude Code Hooks Notifier",
            Visible = true
        };

        _trayIcon.ContextMenuStrip = BuildMenu();

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                StopBlinking();
        };
        _trayIcon.DoubleClick += (_, _) =>
        {
            StopBlinking();
            try
            {
                if (_mainWindow == null || _mainWindow.IsDisposed)
                {
                    _mainWindow = new MainWindow();
                    _mainWindow.Show();
                }
                else
                {
                    _mainWindow.Activate();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Dashboard window error: {ex.Message}");
                ToastService.ShowBalloon("Dashboard Error",
                    $"Could not open dashboard:\n{ex.Message}");
            }
        };

        // ── Blink timer ──────────────────────────────────────────────
        _blinkTimer = new System.Windows.Forms.Timer();
        _blinkTimer.Tick += (_, _) =>
        {
            _isHighlighted = !_isHighlighted;
            _trayIcon.Icon = _isHighlighted ? IconHelper.Highlighted : IconHelper.Normal;
            _blinkTick++;
            if (_blinkTick >= _maxBlinkTicks)
                StopBlinking();
        };

        // ── IPC server ───────────────────────────────────────────────
        IpcService.StartServer(OnIpcMessage, _cts.Token);

        Application.Run();
        return 0;
    }

    // ── IPC message handler (called from background thread) ──────────
    private static void OnIpcMessage(IpcMessage msg)
    {
        if (_uiContext == null) return;

        _uiContext.Post(_ =>
        {
            switch (msg.Type)
            {
                case "toast":
                    // hooks-notify.exe already shows the toast when blinkType="none"
                    var needToast = msg.BlinkType != "none";
                    if (needToast)
                        ToastService.Show(msg.Title, msg.Body);

                    var level = msg.BlinkType switch
                    {
                        "long"  => "P0",
                        "short" => "P0.5",
                        _       => "Toast"
                    };
                    var ticks = msg.BlinkType switch
                    {
                        "long"  => 20,
                        "short" => 10,
                        _       => 0
                    };
                    if (ticks > 0)
                        StartBlinking(ticks);

                    // Record event history
                    var entry = new EventEntry(DateTime.Now, level, msg.EventName,
                        string.IsNullOrEmpty(msg.Body) ? msg.Title : msg.Body);
                    EventHistory.Add(entry);
                    _mainWindow?.PushEvent(entry);
                    break;

                case "stateful":
                    switch (msg.EventName)
                    {
                        case "SubagentStart":
                            _subagentCount++;
                            _lastAgentType = msg.EventType;
                            break;
                        case "SubagentStop":
                            _lastAgentType = "";
                            break;
                        case "TaskCreated":
                            _taskCount++;
                            _lastTaskDesc = msg.EventType;
                            break;
                    }
                    // Record stateful events
                    var sfx = new EventEntry(DateTime.Now, "Stateful", msg.EventName,
                        string.IsNullOrEmpty(msg.Title) ? $"{msg.EventName}: {msg.EventType}" : msg.Title);
                    EventHistory.Add(sfx);
                    _mainWindow?.PushEvent(sfx);
                    UpdateStatusMenu();
                    break;
            }
        }, null);
    }

    // ── Blinking (UI thread only) ────────────────────────────────────
    private static void StartBlinking(int maxTicks = 20)
    {
        _blinkTick = 0;
        _maxBlinkTicks = maxTicks;
        _isHighlighted = false;
        _blinkTimer?.Start();
    }

    private static void StopBlinking()
    {
        _blinkTimer?.Stop();
        _trayIcon!.Icon = IconHelper.Normal;
    }

    // ── Update dynamic menu items ────────────────────────────────────
    private static void UpdateStatusMenu()
    {
        if (_statusCountItem != null)
            _statusCountItem.Text = I18n.Get("menu.notifications", (_taskCount + _subagentCount).ToString());

        if (_statusSubagentItem != null)
            _statusSubagentItem.Text = string.IsNullOrEmpty(_lastAgentType)
                ? I18n.Get("menu.subagent_idle")
                : I18n.Get("menu.subagent", _lastAgentType);

        if (_statusTaskItem != null)
            _statusTaskItem.Text = string.IsNullOrEmpty(_lastTaskDesc)
                ? I18n.Get("menu.task_idle")
                : I18n.Get("menu.task", _lastTaskDesc);
    }

    // ── Context menu ─────────────────────────────────────────────────
    private static ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(new ToolStripMenuItem(I18n.Get("menu.running")) { Enabled = false });

        // Dynamic status section
        _statusCountItem = new ToolStripMenuItem(I18n.Get("menu.notifications", "0")) { Enabled = false };
        _statusSubagentItem = new ToolStripMenuItem(I18n.Get("menu.subagent_idle")) { Enabled = false };
        _statusTaskItem = new ToolStripMenuItem(I18n.Get("menu.task_idle")) { Enabled = false };
        menu.Items.Add(_statusCountItem);
        menu.Items.Add(_statusSubagentItem);
        menu.Items.Add(_statusTaskItem);

        menu.Items.Add(new ToolStripSeparator());

        var configItem = new ToolStripMenuItem(I18n.Get("menu.configure_hooks"));
        configItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(configItem);

        var updatePathItem = new ToolStripMenuItem(I18n.Get("menu.update_hook_path"));
        updatePathItem.Click += (_, _) => AutoConfigureHooks();
        menu.Items.Add(updatePathItem);

        var clearItem = new ToolStripMenuItem(I18n.Get("menu.clear_counters"));
        clearItem.Click += (_, _) => { _subagentCount = 0; _taskCount = 0; _lastAgentType = ""; _lastTaskDesc = ""; UpdateStatusMenu(); };
        menu.Items.Add(clearItem);

        // Language submenu
        var langItem = new ToolStripMenuItem(I18n.Get("menu.language"));
        foreach (var code in I18n.AvailableLanguages)
        {
            var langName = code == "en" ? "English" : "中文";
            var sub = new ToolStripMenuItem(langName)
            {
                Checked = code == I18n.CurrentLanguage,
                CheckState = code == I18n.CurrentLanguage ? CheckState.Checked : CheckState.Unchecked
            };
            var captured = code;
            sub.Click += (_, _) =>
            {
                I18n.SetLanguage(captured);
                RebuildMenu();
            };
            langItem.DropDownItems.Add(sub);
        }
        menu.Items.Add(langItem);

        var autoStartItem = new ToolStripMenuItem(I18n.Get("menu.open_at_login")) { CheckOnClick = true };
        autoStartItem.Checked = IsAutoStartEnabled();
        autoStartItem.CheckedChanged += (_, _) => ToggleAutoStart(autoStartItem.Checked);
        menu.Items.Add(autoStartItem);

        var aboutItem = new ToolStripMenuItem(I18n.Get("menu.about"));
        aboutItem.Click += (_, _) => ShowAbout();
        menu.Items.Add(aboutItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem(I18n.Get("menu.exit"));
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        menu.Opening += (_, _) => StopBlinking();

        return menu;
    }

    // ── Actions ──────────────────────────────────────────────────────
    private static void OpenSettings()
    {
        StopBlinking();
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "settings.json");
        if (File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else
            ToastService.ShowBalloon(I18n.Get("settings.not_found"), path);
    }

    private static void ShowAbout()
    {
        ToastService.ShowBalloon(
            I18n.Get("about.title"),
            I18n.Get("about.version", "1.5.4"));
    }

    /// <summary>Rebuild the entire menu after language switch.</summary>
    private static void RebuildMenu()
    {
        if (_trayIcon == null) return;
        _trayIcon.ContextMenuStrip = BuildMenu();
        UpdateStatusMenu();
    }

    // ── Auto-start ───────────────────────────────────────────────────
    private const string AutoStartKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AutoStartValue = "ClaudeCodeHooksNotifier";

    private static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutoStartKey);
        return key?.GetValue(AutoStartValue) != null;
    }

    private static void ToggleAutoStart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutoStartKey, true);
        if (key == null) return;
        if (enable)
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                key.SetValue(AutoStartValue, $"\"{exe}\" --tray");
        }
        else
        {
            try { key.DeleteValue(AutoStartValue); } catch { }
        }
    }

    // ── Auto-configure hook path ────────────────────────────────────
    private static void AutoConfigureHooks()
    {
        StopBlinking();
        try
        {
            var thisExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(thisExe)) return;

            var psi = new ProcessStartInfo(thisExe, "--configure-hooks")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;

            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit(10000);

            if (proc.ExitCode == 0)
            {
                ToastService.ShowBalloon(I18n.Get("setup.updated", "0", ""),
                    error?.Replace("ERROR:", "")?.Trim() ?? "Done");
            }
            else
            {
                ToastService.ShowBalloon("Update failed",
                    error?.Replace("ERROR:", "")?.Trim() ?? $"Exit code {proc.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowBalloon("Error", ex.Message);
        }
    }

    // ── Clean shutdown ──────────────────────────────────────────────
    private static void Shutdown()
    {
        StopBlinking();
        _cts?.Cancel();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        IconHelper.Cleanup();
        Application.Exit();
    }
}
