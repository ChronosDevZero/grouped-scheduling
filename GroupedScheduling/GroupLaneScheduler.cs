namespace GroupedScheduling;

/// <summary>
/// Exclusive-group lane: one active group at a time, FIFO waiting groups,
/// serial or in-group parallel grants.
/// </summary>
internal sealed class GroupLaneScheduler
{
    private readonly object _gate;
    private readonly Dictionary<EventActionGroup, GroupRuntime> _runtimes = new();
    private readonly List<EventActionGroup> _waitingGroups = new();
    private EventActionGroup? _activeGroup;
    private bool _disposed;

    public GroupLaneScheduler(object gate)
    {
        _gate = gate;
    }

    public async Task AcquireAsync(EventActionGroup group, CancellationToken cancellationToken)
    {
        TaskCompletionSource? waiter = null;
        lock (_gate)
        {
            ThrowIfDisposed();

            if (TryEnterNow_NoLock(group))
                return;

            waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var rt = GetRuntime_NoLock(group);
            rt.Waiters.Enqueue(waiter);

            if (!ReferenceEquals(_activeGroup, group) && !_waitingGroups.Contains(group))
                _waitingGroups.Add(group);
        }

        await using var reg = cancellationToken.Register(
            static state =>
            {
                var (scheduler, g, tcs) = ((GroupLaneScheduler, EventActionGroup, TaskCompletionSource))state!;
                scheduler.CancelWaiter(g, tcs);
            },
            (this, group, waiter));

        await waiter.Task.ConfigureAwait(false);
    }

    public void Release(EventActionGroup group)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            var rt = GetRuntime_NoLock(group);
            if (rt.Running <= 0)
                throw new InvalidOperationException("Release called with no matching Acquire.");

            rt.Running--;

            if (ReferenceEquals(_activeGroup, group) && rt.Waiters.Count > 0)
            {
                GrantWaiters_NoLock(group, rt);
                return;
            }

            if (rt.Running != 0)
                return;

            if (ReferenceEquals(_activeGroup, group))
                _activeGroup = null;

            _waitingGroups.Remove(group);
            PromoteNext_NoLock();
        }
    }

    public void CancelAllWaiters()
    {
        lock (_gate)
        {
            foreach (var rt in _runtimes.Values)
            {
                while (rt.Waiters.Count > 0)
                    rt.Waiters.Dequeue().TrySetCanceled();
            }

            _waitingGroups.Clear();
            _activeGroup = null;
        }
    }

    public void MarkDisposed()
    {
        lock (_gate)
            _disposed = true;
    }

    private bool TryEnterNow_NoLock(EventActionGroup group)
    {
        if (_activeGroup is null)
        {
            _activeGroup = group;
            GetRuntime_NoLock(group).Running++;
            return true;
        }

        if (!ReferenceEquals(_activeGroup, group))
            return false;

        var rt = GetRuntime_NoLock(group);
        if (group.CanRunConcurrentlyInGroup || rt.Running == 0)
        {
            rt.Running++;
            return true;
        }

        return false;
    }

    private void CancelWaiter(EventActionGroup group, TaskCompletionSource waiter)
    {
        lock (_gate)
        {
            if (waiter.Task.IsCompleted)
                return;

            var rt = GetRuntime_NoLock(group);
            if (rt.Waiters.Count > 0)
            {
                var kept = new Queue<TaskCompletionSource>();
                while (rt.Waiters.Count > 0)
                {
                    var w = rt.Waiters.Dequeue();
                    if (!ReferenceEquals(w, waiter))
                        kept.Enqueue(w);
                }

                rt.Waiters = kept;
            }

            if (rt.Waiters.Count == 0 && rt.Running == 0)
                _waitingGroups.Remove(group);

            waiter.TrySetCanceled();
        }
    }

    private void GrantWaiters_NoLock(EventActionGroup group, GroupRuntime rt)
    {
        if (group.CanRunConcurrentlyInGroup)
        {
            while (rt.Waiters.Count > 0)
            {
                rt.Running++;
                rt.Waiters.Dequeue().TrySetResult();
            }
        }
        else if (rt.Waiters.Count > 0)
        {
            rt.Running++;
            rt.Waiters.Dequeue().TrySetResult();
        }
    }

    private void PromoteNext_NoLock()
    {
        while (_waitingGroups.Count > 0)
        {
            var next = _waitingGroups[0];
            _waitingGroups.RemoveAt(0);
            var nrt = GetRuntime_NoLock(next);
            if (nrt.Waiters.Count == 0)
                continue;

            _activeGroup = next;
            GrantWaiters_NoLock(next, nrt);
            return;
        }
    }

    private GroupRuntime GetRuntime_NoLock(EventActionGroup group)
    {
        if (!_runtimes.TryGetValue(group, out var rt))
        {
            rt = new GroupRuntime();
            _runtimes[group] = rt;
        }

        return rt;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ActionsHandler));
    }

    private sealed class GroupRuntime
    {
        public int Running;
        public Queue<TaskCompletionSource> Waiters = new();
    }
}
