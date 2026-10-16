using GroupedScheduling;

namespace GroupedScheduling.Sample;

/// <summary>Static-only scheduled worker (no instance methods / no instance state field).</summary>
public static class StaticOnlyJobs
{
    public static int Hits;
    public static string? LastLabel;

    [ScheduleGroup("static-only", ScheduleMode.ExclusiveSerial)]
    public static async Task BootAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START StaticOnlyJobs.BootAsync");
        Hits++;
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  StaticOnlyJobs.BootAsync Hits={Hits}");
    }

    [ScheduleGroup("static-only", ScheduleMode.ExclusiveSerial)]
    public static async Task LabelAsync(string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START StaticOnlyJobs.LabelAsync label={label}");
        Hits++;
        LastLabel = label;
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  StaticOnlyJobs.LabelAsync");
    }

    [ScheduleGroup("static-only", ScheduleMode.ExclusiveSerial)]
    public static async Task<int> SumAsync(IEnumerable<int> values, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var list = values.ToList();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START StaticOnlyJobs.SumAsync count={list.Count}");
        await Task.Yield();
        var sum = list.Sum();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  StaticOnlyJobs.SumAsync => {sum}");
        return sum;
    }
}
