using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

public sealed class ParallelWorker : IDisposable
{
    public IntervalRecorder Recorder { get; } = new();

    public void Dispose() { }

    [ScheduleGroup("par", ScheduleMode.ExclusiveParallel)]
    public Task P1Async(CancellationToken cancellationToken = default) => Run("P1", cancellationToken);

    [ScheduleGroup("par", ScheduleMode.ExclusiveParallel)]
    public Task P2Async(CancellationToken cancellationToken = default) => Run("P2", cancellationToken);

    [ScheduleGroup("par", ScheduleMode.ExclusiveParallel)]
    public Task P3Async(CancellationToken cancellationToken = default) => Run("P3", cancellationToken);

    private async Task Run(string name, CancellationToken cancellationToken)
    {
        var start = Environment.TickCount64;
        await Task.Delay(40, cancellationToken).ConfigureAwait(false);
        Recorder.Add("par", name, start, Environment.TickCount64);
    }
}
