using System.ComponentModel;

namespace GroupedScheduling;

/// <summary>
/// Shared shutdown/token plumbing for per-instance and per-type scheduling state.
/// Hidden from IntelliSense; not part of the intended authoring API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class SchedulingOwnerState : IDisposable
{
    private readonly CancellationTokenSource _ownerCts = new();
    private int _disposed;

    public ActionsHandler Handler { get; }

    protected CancellationToken OwnerToken => _ownerCts.Token;

    protected SchedulingOwnerState(Dictionary<int, EventActionGroup> groupMap)
    {
        Handler = new ActionsHandler(groupMap);
    }

    public CancellationTokenSource CreateCallToken(CancellationToken methodToken = default)
        => SchedulingRuntime.CreateLinkedCallToken(OwnerToken, methodToken);

    public void Shutdown() => ShutdownAsync().GetAwaiter().GetResult();

    public async Task ShutdownAsync(TimeSpan? drainTimeout = null)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _ownerCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        await Handler.ShutdownAsync(drainTimeout).ConfigureAwait(false);
        _ownerCts.Dispose();
    }

    public void Dispose() => Shutdown();
}
