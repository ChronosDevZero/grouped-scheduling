namespace GroupedScheduling.Fixture.Workers;

/// <summary>Thread-safe start/end ticks for asserting overlap rules.</summary>
public sealed class IntervalRecorder
{
    private readonly object _gate = new();
    private readonly List<(string Group, string Name, long Start, long End)> _items = new();

    public void Add(string group, string name, long start, long end)
    {
        lock (_gate)
            _items.Add((group, name, start, end));
    }

    public IReadOnlyList<(string Group, string Name, long Start, long End)> Snapshot()
    {
        lock (_gate)
            return _items.ToList();
    }

    public void Clear()
    {
        lock (_gate)
            _items.Clear();
    }

    public static void AssertNoOverlap(IEnumerable<(string Group, string Name, long Start, long End)> intervals, string group)
    {
        var ordered = intervals.Where(i => i.Group == group).OrderBy(i => i.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Start < ordered[i - 1].End)
                throw new InvalidOperationException(
                    $"Group '{group}' overlapped: {ordered[i - 1].Name} vs {ordered[i].Name}");
        }
    }

    public static void AssertSomeOverlap(IEnumerable<(string Group, string Name, long Start, long End)> intervals, string group)
    {
        var list = intervals.Where(i => i.Group == group).OrderBy(i => i.Start).ToList();
        if (list.Count < 2)
            throw new InvalidOperationException($"Need ≥2 intervals in '{group}' to assert overlap.");

        for (var i = 0; i < list.Count; i++)
        for (var j = i + 1; j < list.Count; j++)
        {
            if (list[i].Start < list[j].End && list[j].Start < list[i].End)
                return;
        }

        throw new InvalidOperationException($"Expected overlap inside group '{group}'.");
    }

    public static bool Intersects(
        (string Group, string Name, long Start, long End) a,
        (string Group, string Name, long Start, long End) b)
        => a.Start < b.End && b.Start < a.End;
}
