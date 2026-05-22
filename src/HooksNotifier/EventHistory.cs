namespace HooksNotifier;

internal sealed record EventEntry(
    DateTime Timestamp,
    string Level,       // "P0", "P0.5", "Toast", "Stateful"
    string EventName,
    string Summary
);

/// <summary>Ring buffer for recent hook events.</summary>
internal static class EventHistory
{
    private static readonly List<EventEntry> _entries = new();
    private const int MaxEntries = 500;

    public static IReadOnlyList<EventEntry> Entries => _entries.AsReadOnly();

    public static void Add(EventEntry entry)
    {
        lock (_entries)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }
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
}
