using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

/// <summary>Long Delay(ct) work for method-token and dispose cancel coverage.</summary>
public sealed class CancelWorker : IDisposable
{
    public void Dispose() { }

    [ScheduleGroup("hold", ScheduleMode.FullyConcurrent)]
    public async Task HoldAsync(int delayMs, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
    }
}
