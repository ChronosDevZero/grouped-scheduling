using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

public sealed class NestWorker : IDisposable
{
    /// <summary>Set to another instance for cross-handler nest (A→B).</summary>
    public NestWorker? Peer { get; set; }

    public bool PeerWorkRan { get; private set; }

    public void Dispose() { }

    [ScheduleGroup("nest", ScheduleMode.FullyConcurrent)]
    public async Task OuterAsync()
    {
        await InnerAsync();
    }

    [ScheduleGroup("nest", ScheduleMode.FullyConcurrent)]
    public async Task InnerAsync()
    {
        await Task.Yield();
    }

    [ScheduleGroup("nest", ScheduleMode.FullyConcurrent)]
    public async Task CallPeerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Peer is null)
            throw new InvalidOperationException("Peer not set.");
        await Peer.PeerWorkAsync(cancellationToken).ConfigureAwait(false);
    }

    [ScheduleGroup("nest", ScheduleMode.FullyConcurrent)]
    public async Task PeerWorkAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PeerWorkRan = true;
        await Task.Yield();
    }
}
