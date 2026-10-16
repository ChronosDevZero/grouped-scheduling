using GroupedScheduling;

namespace GroupedScheduling.Sample;

/// <summary>
/// Cross-instance nest, cancel-in-flight, exception propagation, and mid-run drain demos.
/// </summary>
public sealed class LifecycleDemoWorker
{
    public LifecycleDemoWorker? Peer { get; set; }
    public bool PeerWorkRan { get; private set; }
    public bool AfterBoomRan { get; private set; }

    [ScheduleGroup("life-nest", ScheduleMode.FullyConcurrent)]
    public async Task CallPeerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START Lifecycle.CallPeerAsync");
        if (Peer is null)
            throw new InvalidOperationException("Peer not set.");
        await Peer.PeerWorkAsync(cancellationToken).ConfigureAwait(false);
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  Lifecycle.CallPeerAsync");
    }

    [ScheduleGroup("life-nest", ScheduleMode.FullyConcurrent)]
    public async Task PeerWorkAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START Lifecycle.PeerWorkAsync");
        PeerWorkRan = true;
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  Lifecycle.PeerWorkAsync");
    }

    [ScheduleGroup("life-hold", ScheduleMode.FullyConcurrent)]
    public async Task HoldAsync(int delayMs, CancellationToken cancellationToken = default)
    {
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START Lifecycle.HoldAsync delay={delayMs}ms");
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  Lifecycle.HoldAsync");
    }

    [ScheduleGroup("life-boom", ScheduleMode.ExclusiveSerial)]
    public async Task BoomAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START Lifecycle.BoomAsync");
        await Task.Yield();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] THROW Lifecycle.BoomAsync");
        throw new InvalidOperationException("sample-boom");
    }

    [ScheduleGroup("life-next", ScheduleMode.ExclusiveSerial)]
    public async Task AfterBoomAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START Lifecycle.AfterBoomAsync");
        AfterBoomRan = true;
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  Lifecycle.AfterBoomAsync");
    }

    [ScheduleGroup("drain", ScheduleMode.ExclusiveSerial)]
    public async Task DrainHoldAsync(int delayMs, CancellationToken cancellationToken = default)
    {
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START Lifecycle.DrainHoldAsync delay={delayMs}ms");
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  Lifecycle.DrainHoldAsync");
    }

    [ScheduleGroup("drain", ScheduleMode.ExclusiveSerial)]
    public async Task DrainQueuedAsync(CancellationToken cancellationToken = default)
    {
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START Lifecycle.DrainQueuedAsync (should not start if stopped while queued)");
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  Lifecycle.DrainQueuedAsync");
    }
}
