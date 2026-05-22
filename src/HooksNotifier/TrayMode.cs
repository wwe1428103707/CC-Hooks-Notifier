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

    public static int Run()
    {
        // Single instance check
        using var mutex = new Mutex(true, MutexName, out var created);
        if (!created)
            return 0; // Another instance is running

        ApplicationConfiguration.Initialize();
        _uiContext = SynchronizationContext.Current; // WindowsForms sync context
        _cts = new CancellationTokenSource();

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
        _trayIcon.DoubleClick += (_, _) => OpenSettings();

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
                    ToastService.Show(msg.Title, msg.Body);
                    var ticks = msg.BlinkType switch
                    {
                        "long"  => 20,  // P0: 10 seconds
                        "short" => 10,  // P0.5: 5 seconds
                        _       => 0    // no blink
                    };
                    if (ticks > 0)
                        StartBlinking(ticks);
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

    // ── Context menu ─────────────────────────────────────────────────
    private static ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(new ToolStripMenuItem("Hooks Notifier — running") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        var configItem = new ToolStripMenuItem("Configure Hooks");
        configItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(configItem);

        var updatePathItem = new ToolStripMenuItem("Update Hook Path");
        updatePathItem.Click += (_, _) => AutoConfigureHooks();
        menu.Items.Add(updatePathItem);

        var autoStartItem = new ToolStripMenuItem("Open at Login") { CheckOnClick = true };
        autoStartItem.Checked = IsAutoStartEnabled();
        autoStartItem.CheckedChanged += (_, _) => ToggleAutoStart(autoStartItem.Checked);
        menu.Items.Add(autoStartItem);

        var aboutItem = new ToolStripMenuItem("About...");
        aboutItem.Click += (_, _) => ShowAbout();
        menu.Items.Add(aboutItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
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
            ToastService.ShowBalloon("Settings not found", path);
    }

    private static void ShowAbout()
    {
        ToastService.ShowBalloon(
            "Claude Code Hooks Notifier",
            "v1.0.0\nBell icon tray + toast notifications");
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
                ToastService.ShowBalloon("Hook path updated",
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
