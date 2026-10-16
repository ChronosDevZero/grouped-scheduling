using GroupedScheduling;

namespace GroupedScheduling.Sample;

/// <summary>
/// Demo methods across four schedule policies:
/// <list type="bullet">
/// <item><c>overlap-a</c> — ExclusiveParallel (3 methods, may overlap with each other)</item>
/// <item><c>serial</c> — ExclusiveSerial (4 methods, one at a time)</item>
/// <item><c>anytime-*</c> — FullyConcurrent (2 methods, run with anything)</item>
/// <item><c>overlap-b</c> — ExclusiveParallel (5 methods, may overlap with each other)</item>
/// </list>
/// Exclusive groups still share one lane: only one exclusive group is active at a time.
/// </summary>
public sealed class ModesDemoWorker
{
    private static readonly Random Rng = Random.Shared;
    private readonly object _intervalGate = new();
    private readonly List<(string Group, string Name, long Start, long End)> _intervals = new();

    /// <summary>Recorded work intervals for post-run assertions.</summary>
    public IReadOnlyList<(string Group, string Name, long Start, long End)> Intervals
    {
        get { lock (_intervalGate) return _intervals.ToList(); }
    }

    public void ClearIntervals()
    {
        lock (_intervalGate) _intervals.Clear();
    }

    /// <summary>Fails if any two ExclusiveSerial intervals overlap.</summary>
    public void AssertSerialNeverOverlapped()
    {
        var serial = Intervals.Where(i => i.Group == "serial").OrderBy(i => i.Start).ToList();
        for (var i = 1; i < serial.Count; i++)
        {
            if (serial[i].Start < serial[i - 1].End)
                throw new InvalidOperationException(
                    $"Serial overlap: {serial[i - 1].Name} [{serial[i - 1].Start}-{serial[i - 1].End}] vs {serial[i].Name} [{serial[i].Start}-{serial[i].End}]");
        }
    }

    /// <summary>Fails if ExclusiveParallel intervals in <paramref name="group"/> never overlapped.</summary>
    public void AssertParallelOverlapped(string group = "overlap-a")
    {
        var list = Intervals.Where(i => i.Group == group).OrderBy(i => i.Start).ToList();
        if (list.Count < 2)
            throw new InvalidOperationException($"Need ≥2 intervals in '{group}' to assert overlap.");

        for (var i = 0; i < list.Count; i++)
        for (var j = i + 1; j < list.Count; j++)
        {
            if (list[i].Start < list[j].End && list[j].Start < list[i].End)
                return;
        }

        throw new InvalidOperationException($"Expected ExclusiveParallel overlap inside group '{group}'.");
    }

    // --- overlap-a: ExclusiveParallel (3) ---

    [ScheduleGroup("overlap-a", ScheduleMode.ExclusiveParallel)]
    public Task OverlapA1Async(CancellationToken cancellationToken = default)
        => RunAsync("overlap-a", nameof(OverlapA1Async), cancellationToken);

    [ScheduleGroup("overlap-a", ScheduleMode.ExclusiveParallel)]
    public Task OverlapA2Async(CancellationToken cancellationToken = default)
        => RunAsync("overlap-a", nameof(OverlapA2Async), cancellationToken);

    [ScheduleGroup("overlap-a", ScheduleMode.ExclusiveParallel)]
    public Task OverlapA3Async(CancellationToken cancellationToken = default)
        => RunAsync("overlap-a", nameof(OverlapA3Async), cancellationToken);

    // --- serial: ExclusiveSerial (4) ---

    [ScheduleGroup("serial", ScheduleMode.ExclusiveSerial)]
    public Task Serial1Async(CancellationToken cancellationToken = default)
        => RunAsync("serial", nameof(Serial1Async), cancellationToken);

    [ScheduleGroup("serial", ScheduleMode.ExclusiveSerial)]
    public Task Serial2Async(CancellationToken cancellationToken = default)
        => RunAsync("serial", nameof(Serial2Async), cancellationToken);

    [ScheduleGroup("serial", ScheduleMode.ExclusiveSerial)]
    public Task Serial3Async(CancellationToken cancellationToken = default)
        => RunAsync("serial", nameof(Serial3Async), cancellationToken);

    [ScheduleGroup("serial", ScheduleMode.ExclusiveSerial)]
    public Task Serial4Async(CancellationToken cancellationToken = default)
        => RunAsync("serial", nameof(Serial4Async), cancellationToken);

    // --- FullyConcurrent (2) ---

    [ScheduleGroup("anytime", ScheduleMode.FullyConcurrent)]
    public Task Anytime1Async(CancellationToken cancellationToken = default)
        => RunAsync("anytime", nameof(Anytime1Async), cancellationToken);

    [ScheduleGroup("anytime", ScheduleMode.FullyConcurrent)]
    public Task Anytime2Async(CancellationToken cancellationToken = default)
        => RunAsync("anytime", nameof(Anytime2Async), cancellationToken);

    // --- overlap-b: ExclusiveParallel (5) ---

    [ScheduleGroup("overlap-b", ScheduleMode.ExclusiveParallel)]
    public Task OverlapB1Async(CancellationToken cancellationToken = default)
        => RunAsync("overlap-b", nameof(OverlapB1Async), cancellationToken);

    [ScheduleGroup("overlap-b", ScheduleMode.ExclusiveParallel)]
    public Task OverlapB2Async(CancellationToken cancellationToken = default)
        => RunAsync("overlap-b", nameof(OverlapB2Async), cancellationToken);

    [ScheduleGroup("overlap-b", ScheduleMode.ExclusiveParallel)]
    public Task OverlapB3Async(CancellationToken cancellationToken = default)
        => RunAsync("overlap-b", nameof(OverlapB3Async), cancellationToken);

    [ScheduleGroup("overlap-b", ScheduleMode.ExclusiveParallel)]
    public Task OverlapB4Async(CancellationToken cancellationToken = default)
        => RunAsync("overlap-b", nameof(OverlapB4Async), cancellationToken);

    [ScheduleGroup("overlap-b", ScheduleMode.ExclusiveParallel)]
    public Task OverlapB5Async(CancellationToken cancellationToken = default)
        => RunAsync("overlap-b", nameof(OverlapB5Async), cancellationToken);

    /// <summary>Proves the weaver closes over business parameters (not just CancellationToken).</summary>
    public string? LastEcho { get; private set; }

    [ScheduleGroup("args", ScheduleMode.ExclusiveSerial)]
    public async Task EchoAsync(string message, int times, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START EchoAsync         message={message}, times={times}");
        LastEcho = $"{message}x{times}";
        for (var i = 0; i < times; i++)
            Console.WriteLine($"  echo[{i}]: {message}");
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  EchoAsync         LastEcho={LastEcho}");
    }

    private async Task RunAsync(string group, string name, CancellationToken cancellationToken)
    {
        var delayMs = Rng.Next(80, 450);
        var startWall = DateTime.Now;
        var startTick = Environment.TickCount64;
        Console.WriteLine($"[{startWall:HH:mm:ss.fff}] START {name,-18} delay={delayMs}ms");
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        var endTick = Environment.TickCount64;
        var doneWall = DateTime.Now;
        lock (_intervalGate)
            _intervals.Add((group, name, startTick, endTick));
        Console.WriteLine($"[{doneWall:HH:mm:ss.fff}] DONE  {name,-18} (elapsed={(doneWall - startWall).TotalMilliseconds:F0}ms)");
    }

    /// <summary>Nested type weave coverage for the sample.</summary>
    public sealed class NestedJobs
    {
        public string? LastJobId { get; private set; }

        [ScheduleGroup("nested-jobs", ScheduleMode.ExclusiveSerial)]
        public async Task RunAsync(string jobId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = DateTime.Now;
            Console.WriteLine($"[{start:HH:mm:ss.fff}] START NestedJobs.RunAsync jobId={jobId}");
            LastJobId = jobId;
            await Task.Yield();
            var done = DateTime.Now;
            Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  NestedJobs.RunAsync jobId={LastJobId}");
        }
    }
}
