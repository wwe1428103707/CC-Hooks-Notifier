using System.Diagnostics;
using Microsoft.Win32;

namespace HooksNotifier;

internal partial class MainWindow
{
    private TabPage BuildSettingsTab()
    {
        var page = new TabPage(I18n.Get("tab.settings")) { BackColor = BgPage };
        int y = 20;

        // ── Language ─────────────────────────────────────────────────
        var langCard = CreateCard(20, y, 440, 60);
        langCard.Controls.Add(new Label
        {
            Text = I18n.Get("settings.language"),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, 8),
            Size = new Size(200, 18)
        });
        var langCombo = new ComboBox
        {
            Location = new Point(16, 30),
            Size = new Size(180, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9)
        };
        foreach (var code in I18n.AvailableLanguages)
        {
            var name = code == "en" ? "English" : "中文";
            langCombo.Items.Add(name);
            if (code == I18n.CurrentLanguage)
                langCombo.SelectedIndex = langCombo.Items.Count - 1;
        }
        langCombo.SelectedIndexChanged += (_, _) =>
        {
            var code = langCombo.SelectedIndex == 0 ? "en" : "zh";
            I18n.SetLanguage(code);
            UpdateStatusBar();
        };
        langCard.Controls.Add(langCombo);
        page.Controls.Add(langCard);

        y += 76;

        // ── Auto-start ───────────────────────────────────────────────
        var autoCard = CreateCard(20, y, 440, 60);
        autoCard.Controls.Add(new Label
        {
            Text = I18n.Get("settings.auto_start"),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, 8),
            Size = new Size(200, 18)
        });
        var autoBox = new CheckBox
        {
            Text = I18n.Get("settings.auto_start"),
            Location = new Point(16, 32),
            Size = new Size(280, 22),
            Checked = IsAutoStartEnabled()
        };
        autoBox.CheckedChanged += (_, _) => ToggleAutoStart(autoBox.Checked);
        autoCard.Controls.Add(autoBox);
        page.Controls.Add(autoCard);

        y += 76;

        // ── Hook path ────────────────────────────────────────────────
        var pathCard = CreateCard(20, y, 880, 80);
        pathCard.Controls.Add(new Label
        {
            Text = I18n.Get("settings.hook_path"),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, 8),
            Size = new Size(300, 18)
        });
        var exePath = Environment.ProcessPath ?? "";
        var pathBox = new TextBox
        {
            Text = exePath,
            Location = new Point(16, 32),
            Size = new Size(720, 22),
            BackColor = Color.FromArgb(240, 242, 245),
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            Font = new Font("Consolas", 9)
        };
        pathCard.Controls.Add(pathBox);

        var updateBtn = new Button
        {
            Text = I18n.Get("settings.update"),
            Location = new Point(746, 30),
            Size = new Size(110, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            FlatAppearance = { BorderColor = AccentBlue }
        };
        updateBtn.Click += (_, _) => AutoConfigureHooks();
        pathCard.Controls.Add(updateBtn);

        var openBtn = new Button
        {
            Text = I18n.Get("settings.open_file"),
            Location = new Point(16, 62),
            Size = new Size(110, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            FlatAppearance = { BorderColor = BorderLight }
        };
        openBtn.Click += (_, _) =>
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "settings.json");
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        };
        pathCard.Controls.Add(openBtn);
        page.Controls.Add(pathCard);

        return page;
    }

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
