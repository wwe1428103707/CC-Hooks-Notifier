using System.Text.Json;

namespace HooksNotifier;

internal sealed record EventEntry(
    DateTime Timestamp,
    string Level,
    string EventName,
    string Summary,
    string Detail = ""
);

/// <summary>Persistent ring buffer for hook events. Survives process restarts.</summary>
internal static class EventHistory
{
    private static readonly List<EventEntry> _entries = new();
    private const int MaxEntries = 500;
    private const string FileName = "event_history.json";

    private static readonly string FilePath;

    static EventHistory()
    {
        var dir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(dir))
            dir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        FilePath = Path.Combine(dir, FileName);
        LoadFromFile();
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
