using System.Diagnostics;

namespace GroupedScheduling;

/// <summary>
/// Schedules async work into named groups. Exclusive groups share one lane (FIFO activation);
/// within the active group, work is serial or parallel per <see cref="ScheduleMode"/>.
/// <see cref="ScheduleMode.FullyConcurrent"/> bypasses the exclusive lane.
/// </summary>
public sealed class ActionsHandler : IDisposable
{
    private readonly Dictionary<int, EventActionGroup> _groupMap;
    private readonly object _gate = new();
    private readonly HashSet<Task> _inFlight = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly GroupLaneScheduler _lane;

    private int _shuttingDown;
    private bool _disposed;

    public ActionsHandler(Dictionary<int, EventActionGroup> groupMap)
    {
        ArgumentNullException.ThrowIfNull(groupMap);
        _groupMap = groupMap;
        _lane = new GroupLaneScheduler(_gate);
    }

    public bool IsShuttingDown => Volatile.Read(ref _shuttingDown) != 0;

    

    public Task ScheduleTask(Func<Task> taskFunc, int identifier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskFunc);
        if (IsShuttingDown)
            return Task.FromException(new InvalidOperationException("Scheduler is shutting down."));

        SchedulingNest.ThrowIfActive(this);
        return TrackInFlight(ScheduleCoreAsync(taskFunc, ResolveGroup(identifier), cancellationToken));
    }

    public Task<T> ScheduleTask<T>(Func<Task<T>> taskFunc, int identifier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskFunc);
        if (IsShuttingDown)
            return Task.FromException<T>(new InvalidOperationException("Scheduler is shutting down."));

        SchedulingNest.ThrowIfActive(this);
        return TrackInFlight(ScheduleCoreAsync(taskFunc, ResolveGroup(identifier), cancellationToken));
    }

    private async Task ScheduleCoreAsync(Func<Task> taskFunc, EventActionGroup group, CancellationToken cancellationToken)
    {
        if (group.CanRunConcurrently)
        {
            await RunWithNestAsync(taskFunc).ConfigureAwait(false);
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
        await _lane.AcquireAsync(group, linked.Token).ConfigureAwait(false);
        try
        {
            await RunWithNestAsync(taskFunc).ConfigureAwait(false);
        }
        finally
        {
            _lane.Release(group);
        }
    }

    private async Task<T> ScheduleCoreAsync<T>(Func<Task<T>> taskFunc, EventActionGroup group, CancellationToken cancellationToken)
    {
        if (group.CanRunConcurrently)
            return await RunWithNestAsync(taskFunc).ConfigureAwait(false);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
        await _lane.AcquireAsync(group, linked.Token).ConfigureAwait(false);
        try
        {
            return await RunWithNestAsync(taskFunc).ConfigureAwait(false);
        }
        finally
        {
            _lane.Release(group);
        }
    }

    private EventActionGroup ResolveGroup(int identifier)
    {
        if (!_groupMap.TryGetValue(identifier, out var group))
            throw new ArgumentException($"No task group found for identifier: {identifier}", nameof(identifier));
        return group;
    }

    private async Task RunWithNestAsync(Func<Task> taskFunc)
    {
        using (SchedulingNest.Enter(this))
            await taskFunc().ConfigureAwait(false);
    }

    private async Task<T> RunWithNestAsync<T>(Func<Task<T>> taskFunc)
    {
        using (SchedulingNest.Enter(this))
            return await taskFunc().ConfigureAwait(false);
    }

    private Task TrackInFlight(Task task)
    {
        lock (_gate)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ActionsHandler));
            _inFlight.Add(task);
        }

        _ = task.ContinueWith(
            static (t, state) =>
            {
                var (handler, tracked) = ((ActionsHandler, Task))state!;
                lock (handler._gate)
                    handler._inFlight.Remove(tracked);
            },
            (this, task),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return task;
    }

    private Task<T> TrackInFlight<T>(Task<T> task)
    {
        TrackInFlight((Task)task);
        return task;
    }

    /// <summary>Preferred over setting a flag: cancel waiters, drain in-flight, then dispose.</summary>
    public async Task ShutdownAsync(TimeSpan? drainTimeout = null)
    {
        if (Interlocked.Exchange(ref _shuttingDown, 1) == 0)
        {
            try
            {
                _shutdownCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        Task[] inflight;
        lock (_gate)
            inflight = _inFlight.ToArray();

        if (inflight.Length > 0)
        {
            var all = Task.WhenAll(inflight);
            if (drainTimeout is { } timeout && timeout >= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            {
                var completed = await Task.WhenAny(all, Task.Delay(timeout)).ConfigureAwait(false);
                if (completed != all)
                {
                    Debug.WriteLine(
                        $"GroupedScheduling: ActionsHandler drain timed out after {timeout}; disposing anyway. " +
                        "In-flight work that ignores cancellation may still be running.");
                }
                else
                {
                    try { await all.ConfigureAwait(false); }
                    catch { /* individual tasks already observed by callers */ }
                }
            }
            else
            {
                try { await all.ConfigureAwait(false); }
                catch { /* individual tasks already observed by callers */ }
            }
        }

        Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _lane.MarkDisposed();
        }

        Interlocked.Exchange(ref _shuttingDown, 1);
        try
        {
            _shutdownCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _shutdownCts.Dispose();
        _lane.CancelAllWaiters();
    }
}
