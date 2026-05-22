using System.Diagnostics;
using Microsoft.Win32;

namespace HooksNotifier;

internal partial class MainWindow
{
    private TabPage BuildSettingsTab()
    {
        var page = new TabPage(I18n.Get("tab.settings"));
        int y = 20;

        // Language
        AddSectionLabel(page, ref y, I18n.Get("settings.language"));
        var langCombo = new ComboBox
        {
            Location = new Point(24, y),
            Size = new Size(200, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9)
        };
        var currentLang = I18n.CurrentLanguage;
        foreach (var code in I18n.AvailableLanguages)
        {
            var name = code == "en" ? "English" : "中文";
            langCombo.Items.Add(name);
            if (code == currentLang) langCombo.SelectedIndex = langCombo.Items.Count - 1;
        }
        var langChanged = false;
        langCombo.SelectedIndexChanged += (_, _) =>
        {
            var code = langCombo.SelectedIndex == 0 ? "en" : "zh";
            I18n.SetLanguage(code);
            langChanged = true;
            UpdateStatusBar();
        };
        page.Controls.Add(langCombo);
        y += 34;

        // Auto-start
        AddSectionLabel(page, ref y, I18n.Get("settings.auto_start"));
        var autoStartBox = new CheckBox
        {
            Text = I18n.Get("settings.auto_start"),
            Location = new Point(24, y),
            Size = new Size(300, 24),
            Checked = IsAutoStartEnabled()
        };
        autoStartBox.CheckedChanged += (_, _) => ToggleAutoStart(autoStartBox.Checked);
        page.Controls.Add(autoStartBox);
        y += 34;

        // Hook path
        AddSectionLabel(page, ref y, I18n.Get("settings.hook_path"));
        var exePath = Environment.ProcessPath ?? "";
        var pathBox = new TextBox
        {
            Text = exePath,
            Location = new Point(24, y),
            Size = new Size(600, 24),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true
        };
        page.Controls.Add(pathBox);

        var updateBtn = new Button
        {
            Text = I18n.Get("settings.update"),
            Location = new Point(634, y - 1),
            Size = new Size(90, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(67, 97, 238),
            ForeColor = Color.White
        };
        updateBtn.Click += (_, _) => AutoConfigureHooks();
        page.Controls.Add(updateBtn);
        y += 34;

        // Open settings file
        var openBtn = new Button
        {
            Text = I18n.Get("settings.open_file"),
            Location = new Point(24, y),
            Size = new Size(140, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
        openBtn.Click += (_, _) =>
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "settings.json");
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        };
        page.Controls.Add(openBtn);

        // Notify user when language was changed
        if (langChanged)
        {
            var hint = new Label
            {
                Text = "Language will fully apply after window restart.",
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(24, y),
                Size = new Size(400, 20)
            };
            page.Controls.Add(hint);
        }

        return page;
    }

    private static void AddSectionLabel(TabPage page, ref int y, string text)
    {
        page.Controls.Add(new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(24, y),
            Size = new Size(400, 18)
        });
        y += 22;
    }

    private void UpdateStatusBar()
    {
        _statusBar.Text = $"  {I18n.Get("dashboard.service_running")}  |  {I18n.Get("settings.language")}: {I18n.CurrentLanguage.ToUpper()}  |  v1.4.0";
    }

    // Reuse existing auto-start logic from TrayMode
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

    private static void AutoConfigureHooks()
    {
        try
        {
            var thisExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(thisExe)) return;
            var psi = new ProcessStartInfo(thisExe, "--configure-hooks")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(10000);
        }
        catch { }
    }
}
