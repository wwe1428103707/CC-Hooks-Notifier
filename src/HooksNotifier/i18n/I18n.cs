using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

namespace HooksNotifier;

/// <summary>
/// Lightweight i18n translation service.
/// Loads JSON string tables from the i18n/ directory alongside the EXE.
/// Supports runtime language switching and fallback to English.
/// </summary>
internal static class I18n
{
    private const string LangRegistryKey = @"Software\ClaudeCode\HooksNotifier";
    private const string LangRegistryValue = "Language";

    private static Dictionary<string, string> _strings = new();
    private static Dictionary<string, string> _fallbackStrings = new();
    private static string _currentLang = "en";

    /// <summary>Current language code (e.g. "en", "zh").</summary>
    public static string CurrentLanguage => _currentLang;

    /// <summary>Available language codes.</summary>
    public static string[] AvailableLanguages => ["en", "zh"];

    /// <summary>Initialize from saved preference or system language.</summary>
    static I18n()
    {
        // Try saved preference first
        var saved = ReadSavedLanguage();
        if (!string.IsNullOrEmpty(saved) && TryLoad(saved))
            return;

        // Fall back to system language
        var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (TryLoad(system))
            return;

        // Final fallback
        TryLoad("en");
    }

    /// <summary>Get a translated string by key.</summary>
    public static string Get(string key)
    {
        if (_strings.TryGetValue(key, out var val))
            return val;
        if (_fallbackStrings.TryGetValue(key, out var fb))
            return fb;
        return $"?{key}?";
    }

    /// <summary>Get a translated string with format arguments.</summary>
    public static string Get(string key, params object?[] args)
    {
        var template = Get(key);
        return string.Format(template, args);
    }

    /// <summary>Switch language at runtime. Returns true if successful.</summary>
    public static bool SetLanguage(string code)
    {
        if (code == _currentLang) return true;
        if (!TryLoad(code)) return false;

        SaveLanguage(code);
        return true;
    }

    // ── Load / Save ──────────────────────────────────────────────────

    private static bool TryLoad(string code)
    {
        try
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath);
            if (string.IsNullOrEmpty(dir))
                dir = AppContext.BaseDirectory;

            var path = Path.Combine(dir, "i18n", $"{code}.json");
            if (!File.Exists(path))
            {
                Log.Error($"I18n: {code}.json not found at {path}");
                return false;
            }

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (entries == null) { Log.Error($"I18n: {code}.json is empty"); return false; }

            // Load the target language
            _strings = entries;
            _currentLang = code;

            // Ensure fallback to English
            if (code != "en")
            {
                var fbPath = Path.Combine(dir, "i18n", "en.json");
                if (File.Exists(fbPath))
                {
                    var fbJson = File.ReadAllText(fbPath);
                    _fallbackStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(fbJson)
                        ?? new Dictionary<string, string>();
                }
            }
            else
            {
                _fallbackStrings = new Dictionary<string, string>();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadSavedLanguage()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LangRegistryKey);
            return key?.GetValue(LangRegistryValue) as string;
        }
        catch { return null; }
    }

    private static void SaveLanguage(string code)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(LangRegistryKey);
            key?.SetValue(LangRegistryValue, code);
        }
        catch { }
    }
}
