using GroupedScheduling;

namespace GroupedScheduling.Sample;

/// <summary>ValueTask / ValueTask&lt;T&gt; weave coverage with visible logging.</summary>
public sealed class ValueTaskDemoWorker
{
    public string? LastPing { get; private set; }
    public int LastScore { get; private set; }

    [ScheduleGroup("vt", ScheduleMode.ExclusiveSerial)]
    public async ValueTask PingAsync(string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START ValueTask.PingAsync  label={label}");
        await Task.Delay(40, cancellationToken).ConfigureAwait(false);
        LastPing = label;
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  ValueTask.PingAsync  LastPing={LastPing}");
    }

    [ScheduleGroup("vt", ScheduleMode.ExclusiveSerial)]
    public async ValueTask<int> ScoreAsync(int baseScore, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START ValueTask.ScoreAsync base={baseScore}");
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        LastScore = baseScore * 2;
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  ValueTask.ScoreAsync LastScore={LastScore}");
        return LastScore;
    }

    /// <summary>Multi-value result via Task&lt;(T1,T2)&gt; — already Task&lt;T&gt; under the hood.</summary>
    [ScheduleGroup("vt", ScheduleMode.ExclusiveSerial)]
    public async Task<(string Name, int Count)> PairAsync(string name, int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START Task<(,)>.PairAsync   name={name}, count={count}");
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  Task<(,)>.PairAsync   => ({name}, {count})");
        return (name, count);
    }
}
