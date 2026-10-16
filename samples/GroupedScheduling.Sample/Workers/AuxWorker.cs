using GroupedScheduling;

namespace GroupedScheduling.Sample;

/// <summary>Second DI-registered worker so the sample proves multi-worker host stop.</summary>
public sealed class AuxWorker : IDisposable
{
    public bool LastPing { get; private set; }

    public void Dispose() { }

    [ScheduleGroup("aux", ScheduleMode.ExclusiveSerial)]
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastPing = true;
        await Task.Yield();
    }
}
