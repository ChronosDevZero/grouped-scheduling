using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

/// <summary>Static-only scheduled surface (no instance methods).</summary>
public static class StaticWorker
{
    public static int Hits;
    public static string? LastLabel;

    [ScheduleGroup("static", ScheduleMode.ExclusiveSerial)]
    public static async Task HitAsync()
    {
        Hits++;
        await Task.Yield();
    }

    [ScheduleGroup("static", ScheduleMode.ExclusiveSerial)]
    public static async Task HitWithLabelAsync(string label)
    {
        Hits++;
        LastLabel = label;
        await Task.Yield();
    }

    [ScheduleGroup("static", ScheduleMode.ExclusiveSerial)]
    public static async Task<int> CountAsync(IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return values.Count();
    }
}

