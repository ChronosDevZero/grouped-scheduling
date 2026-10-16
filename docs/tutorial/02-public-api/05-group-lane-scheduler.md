# File: `GroupLaneScheduler.cs`

**Path:** `GroupedScheduling/GroupLaneScheduler.cs`  
**Visibility:** `internal`

## Why this file exists

This is the **exclusive-group lane**: the algorithm that makes `ExclusiveSerial` and `ExclusiveParallel` real.

Responsibilities:

- At most one **active exclusive group** at a time.
- Waiting groups activate in **FIFO** order.
- Inside the active group, either one runner (serial) or many (parallel).
- Waiters can be **canceled** (caller token or shutdown).
- Dispose path cancels everyone waiting.

`FullyConcurrent` never calls this type.

## Concepts you need first

| Concept | Role |
|---|---|
| Monitor (`lock _gate`) | All lane mutations are under the shared gate |
| `TaskCompletionSource` | Async wait handle for “you may enter now” |
| `RunContinuationsAsynchronously` | Avoid running waiter continuations *inside* the lock |
| FIFO list `_waitingGroups` | Which group is next when the active group drains |
| Per-group `GroupRuntime` | `Running` count + queue of waiters |

## State machine (mental picture)

![Exclusive lane](../assets/03-exclusive-lane.png)

```text
  _activeGroup == null
        |
        | first exclusive acquire
        v
  _activeGroup == G, Running >= 1
        |
        | more acquires for G
        |   serial: queue waiters if Running > 0
        |   parallel: Running++ immediately
        |
        | acquire for other group H
        v
  H goes to _waitingGroups (FIFO), its waiters park on TCS
        |
        | G's last Running hits 0
        v
  PromoteNext: activate head of _waitingGroups, grant waiters
```

## Fields

```csharp
private readonly object _gate;
private readonly Dictionary<EventActionGroup, GroupRuntime> _runtimes = new();
private readonly List<EventActionGroup> _waitingGroups = new();
private EventActionGroup? _activeGroup;
private bool _disposed;
```

| Field | Job |
|---|---|
| `_gate` | Same object as `ActionsHandler`’s gate |
| `_runtimes` | Per-group running count + waiter queue |
| `_waitingGroups` | FIFO of groups that want the lane but are not active |
| `_activeGroup` | The exclusive group currently holding the lane |
| `_disposed` | Reject new acquires |

### Nested `GroupRuntime`

```csharp
private sealed class GroupRuntime
{
    public int Running;
    public Queue<TaskCompletionSource> Waiters = new();
}
```

- `Running` — how many acquired slots are outstanding for this group.
- `Waiters` — parked acquires that will be granted later.

## Constructor

```csharp
public GroupLaneScheduler(object gate) => _gate = gate;
```

Does not allocate its own lock — coordination with the handler requires the shared monitor.

## `AcquireAsync` — enter or wait

```csharp
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
```

### Phase A — under the lock

1. Disposed? throw (`ObjectDisposedException` named as `ActionsHandler` — caller-facing type).
2. `TryEnterNow_NoLock` — if we can run immediately, **return** (no await).
3. Otherwise create a TCS, enqueue it, and ensure the group is on `_waitingGroups` **once**.

**Why `RunContinuationsAsynchronously`?** When someone `TrySetResult` under a lock (or soon after), you do not want the waiter’s continuation to run synchronously while the lane still holds locks or is mid-mutation.

### Phase B — outside the lock

Register cancellation to `CancelWaiter`, then `await waiter.Task`.

**Critical:** the await happens **outside** `lock`. Never park a thread inside the monitor.

## `TryEnterNow_NoLock`

```csharp
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
```

Cases:

1. **Lane free** — become active, `Running++`, success.
2. **Another group active** — fail (caller will wait).
3. **This group active:**
   - Parallel mode → always `Running++`.
   - Serial mode → only if `Running == 0` (normally false if you are active with work; waiters cover the rest). Note: `Running == 0` while still active is an edge during transitions; the important serial rule is “do not increment when someone is already running.”

## `Release`

```csharp
public void Release(EventActionGroup group)
{
    lock (_gate)
    {
        if (_disposed) return;

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
```

### Step by step

1. Ignore if disposed (shutdown races).
2. Decrement `Running`; mismatch → bug (unbalanced release).
3. **If still the active group and peers are waiting:** grant waiters (serial: one; parallel: all) and return — stay active.
4. If `Running` still greater than 0 — other in-group work still holds the lane; done.
5. Else clear active, remove from waiting list, **promote** the next FIFO group.

**Why grant waiters before promoting others?** In-group waiters have priority over other groups while this group is active — that is what “active group” means.

## `GrantWaiters_NoLock`

```csharp
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
```

- Parallel: wake **all** parked peers.
- Serial: wake **exactly one**.

Each grant increments `Running` so `Release` stays balanced.

## `PromoteNext_NoLock`

```csharp
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
```

Skip groups that somehow have no waiters left (canceled). Activate the first group that still has waiters and grant.

## Cancellation: `CancelWaiter`

When the linked token fires:

1. If TCS already completed, no-op.
2. Rebuild the waiter queue **without** this TCS (linear filter).
3. If no waiters and nothing running, remove group from `_waitingGroups`.
4. `TrySetCanceled` on the TCS.

**Why rebuild the queue?** `Queue<T>` has no efficient arbitrary remove; correctness beats micro-optimization on cancel paths.

## `CancelAllWaiters` / `MarkDisposed`

Shutdown/dispose:

- `MarkDisposed` — future acquires throw.
- `CancelAllWaiters` — empty every waiter queue with cancel, clear waiting list, clear active group.

## Worked example (serial)

Groups `A` (serial) and `B` (serial).

1. Task A1 acquires → active=A, Running(A)=1.
2. Task A2 acquires → cannot enter (serial) → waiter; A already active so **not** duplicated on waiting list.
3. Task B1 acquires → different group → waiter; B appended to `_waitingGroups`.
4. A1 releases → Running(A)=0 → waiters for A exist → grant A2 (Running=1), still active=A.
5. A2 releases → Running=0, no waiters → active=null → promote B → grant B1.

## Worked example (parallel in-group)

Group `P` is `ExclusiveParallel`.

1. P1 acquires → active=P, Running=1.
2. P2 acquires → same active + parallel → Running=2 immediately.
3. Other group Q waits on `_waitingGroups`.
4. P1 releases → Running=1 → still running, no promote.
5. P2 releases → Running=0 → promote Q.

## What breaks if…

| Change | Failure |
|---|---|
| Await inside `lock` | Deadlocks / thread-pool starvation |
| FIFO list becomes a set with no order | Unfair activation; surprising latency |
| Serial grants all waiters | Overlapping serial work — data races in user code |
| Forget `Running++` on grant | Unbalanced release / premature promote |
| Compare groups by name instead of reference | Duplicate instances confuse active/waiting |

## See also

- `ActionsHandler.ScheduleCoreAsync` — only caller of Acquire/Release in production
- Engine tests for lanes / modes / cancel
- Sample: `ModesDemoWorker`, `ParallelOverlapSmoke`
