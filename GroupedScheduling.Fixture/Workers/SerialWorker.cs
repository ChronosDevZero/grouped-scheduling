using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

public sealed class SerialWorker : IDisposable
{
    public IntervalRecorder Recorder { get; } = new();

    // Weaver wraps this to shut down scheduling state.
    public void Dispose() { }

    [ScheduleGroup("serial", ScheduleMode.ExclusiveSerial)]
    public Task S1Async(CancellationToken cancellationToken = default) => Run("S1", cancellationToken);

    [ScheduleGroup("serial", ScheduleMode.ExclusiveSerial)]
    public Task S2Async(CancellationToken cancellationToken = default) => Run("S2", cancellationToken);

    [ScheduleGroup("serial", ScheduleMode.ExclusiveSerial)]
    public Task S3Async(CancellationToken cancellationToken = default) => Run("S3", cancellationToken);

    [ScheduleGroup("serial", ScheduleMode.ExclusiveSerial)]
    public Task S4Async(CancellationToken cancellationToken = default) => Run("S4", cancellationToken);

    private async Task Run(string name, CancellationToken cancellationToken)
    {
        var start = Environment.TickCount64;
        await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        Recorder.Add("serial", name, start, Environment.TickCount64);
    }
}
