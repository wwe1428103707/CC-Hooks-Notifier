using System.Text.Json;

namespace HooksNotifier;

internal sealed record EventEntry(
    DateTime Timestamp,
    string Level,
    string EventName,
    string Summary,
    string Detail = "",
    bool IsRead = false
);

/// <summary>Persistent ring buffer for hook events. Survives process restarts.</summary>
internal static class EventHistory
{
    private static readonly List<EventEntry> _entries = new();
    private const string FileName = "event_history.json";
    private const int DefaultMaxEntries = 500;

    private static int _maxEntries = DefaultMaxEntries;

    public static int MaxEntries
    {
        get => _maxEntries;
        set
        {
            _maxEntries = Math.Max(50, Math.Min(value, 10000));
            SaveMaxEntriesToRegistry();
            TrimExcess();
            SaveToFile();
        }
    }

    private static readonly string FilePath;

    static EventHistory()
    {
        var dir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(dir))
            dir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        FilePath = Path.Combine(dir, FileName);
        _maxEntries = LoadMaxEntriesFromRegistry();
        LoadFromFile();
        TrimExcess(); // ensure loaded data respects saved limit
    }

    public static IReadOnlyList<EventEntry> Entries => _entries.AsReadOnly();

    public static void Add(EventEntry entry)
    {
        lock (_entries)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }
        SaveToFile();
    }

    public static EventEntry[] GetRecent(int count)
    {
        lock (_entries)
        {
            var take = Math.Min(count, _entries.Count);
            return _entries.Skip(_entries.Count - take).Take(take).ToArray();
        }
    }

    public static void Clear()
    {
        lock (_entries)
            _entries.Clear();
        SaveToFile();
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }

    private static void TrimExcess()
    {
        lock (_entries)
        {
            if (_entries.Count > _maxEntries)
                _entries.RemoveRange(0, _entries.Count - _maxEntries);
        }
    }

    public static int UnreadCount
    {
        get
        {
            lock (_entries)
            {
                int n = 0;
                foreach (var e in _entries)
                    if (!e.IsRead) n++;
                return n;
            }
        }
    }

    public static int UnreadCountByLevel(string level)
    {
        lock (_entries)
        {
            int n = 0;
            foreach (var e in _entries)
                if (!e.IsRead && e.Level == level)
                    n++;
            return n;
        }
    }

    public static void MarkAllRead()
    {
        lock (_entries)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].IsRead)
                    _entries[i] = _entries[i] with { IsRead = true };
            }
        }
        SaveToFile();
    }

    public static void MarkRead(int index)
    {
        lock (_entries)
        {
            if (index < 0 || index >= _entries.Count) return;
            if (!_entries[index].IsRead)
                _entries[index] = _entries[index] with { IsRead = true };
        }
        SaveToFile();
    }

    public static List<EventEntry> GetUnread()
    {
        lock (_entries)
        {
            return _entries.Where(e => !e.IsRead).ToList();
        }
    }

    public static (int total, int p0, int p05, int toast, int stateful) Counts
    {
        get
        {
            lock (_entries)
            {
                int p0 = 0, p05 = 0, toast = 0, stateful = 0;
                foreach (var e in _entries)
                {
                    switch (e.Level)
                    {
                        case "P0": p0++; break;
                        case "P0.5": p05++; break;
                        case "Toast": toast++; break;
                        default: stateful++; break;
                    }
                }
                return (_entries.Count, p0, p05, toast, stateful);
            }
        }
    }

    // ── Registry persistence for max entries ────────────────────────
    private const string RegKey = @"Software\ClaudeCode\HooksNotifier";
    private const string RegValue = "MaxEntries";

    private static int LoadMaxEntriesFromRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegKey);
            var val = key?.GetValue(RegValue);
            return val is int n ? Math.Max(50, Math.Min(n, 10000)) : DefaultMaxEntries;
        }
        catch { return DefaultMaxEntries; }
    }

    private static void SaveMaxEntriesToRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegKey);
            key?.SetValue(RegValue, _maxEntries);
        }
        catch { }
    }

    // ── File persistence ────────────────────────────────────────────
    private static void LoadFromFile()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            var entries = JsonSerializer.Deserialize<List<EventEntry>>(json);
            if (entries == null) return;
            lock (_entries)
            {
                _entries.Clear();
                _entries.AddRange(entries);
            }
        }
        catch
        {
            // Corrupted file — start fresh
        }
    }

    private static void SaveToFile()
    {
        try
        {
            List<EventEntry> snapshot;
            lock (_entries)
            {
                if (_entries.Count == 0) return;
                snapshot = new List<EventEntry>(_entries);
            }
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Log.Error($"EventHistory save failed: {ex.Message}");
        }
    }
}
