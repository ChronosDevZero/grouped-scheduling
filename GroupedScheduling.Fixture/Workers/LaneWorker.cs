using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

/// <summary>
/// Two exclusive groups + FullyConcurrent — for cross-group lane and anytime-bypass asserts.
/// </summary>
public sealed class LaneWorker : IDisposable
{
    public IntervalRecorder Recorder { get; } = new();

    public void Dispose() { }

    [ScheduleGroup("ex-a", ScheduleMode.ExclusiveSerial)]
    public Task ExAAsync(int holdMs, CancellationToken cancellationToken = default)
        => Run("ex-a", "ExA", holdMs, cancellationToken);

    [ScheduleGroup("ex-b", ScheduleMode.ExclusiveSerial)]
    public Task ExBAsync(int holdMs, CancellationToken cancellationToken = default)
        => Run("ex-b", "ExB", holdMs, cancellationToken);

    [ScheduleGroup("anytime", ScheduleMode.FullyConcurrent)]
    public Task AnytimeAsync(int holdMs, CancellationToken cancellationToken = default)
        => Run("anytime", "Anytime", holdMs, cancellationToken);

    private async Task Run(string group, string name, int holdMs, CancellationToken cancellationToken)
    {
        var start = Environment.TickCount64;
        await Task.Delay(holdMs, cancellationToken).ConfigureAwait(false);
        Recorder.Add(group, name, start, Environment.TickCount64);
    }
}
