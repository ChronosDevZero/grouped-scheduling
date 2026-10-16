# File: `ActionsHandler.cs`

**Path:** `GroupedScheduling/ActionsHandler.cs`

## Why this file exists

`ActionsHandler` is the **public scheduler façade**:

- Resolve a group id to an `EventActionGroup`.
- Reject work during shutdown.
- Enforce nest checks before accepting work.
- Either bypass the lane (`FullyConcurrent`) or acquire/release it.
- Track in-flight tasks for drain-on-shutdown.
- Dispose / cancel waiters cleanly.

If you understand this file, you understand what woven methods ultimately call.

## Concepts you need first

| Concept | Role here |
|---|---|
| `lock` + shared `_gate` | Handler and lane share one monitor for coordinated state |
| `Volatile` / `Interlocked` | Shutdown flag without always taking the lock |
| `CancellationTokenSource` | Shutdown cancels exclusive waiters (linked tokens) |
| `Task.ContinueWith` | Remove finished work from `_inFlight` |
| `ConfigureAwait(false)` | Library code avoids capturing a sync context |
| Nest (`SchedulingNest`) | Same-handler re-entry detection |

## Who calls whom

![Runtime call graph](../assets/02-runtime-call-graph.png)

```text
  SchedulingWeaveHooks.Schedule*  (or test code)
              |
              v
      ScheduleTask / ScheduleTask<T>
              |
              +--> ThrowIfActive (nest)
              +--> TrackInFlight(...)
                        |
                        v
                 ScheduleCoreAsync
                    |            \
                    |             \-- FullyConcurrent --> RunWithNestAsync
                    v
              lane.AcquireAsync
                    |
              RunWithNestAsync (your work)
                    |
              lane.Release
```

## Fields (the object’s memory)

```csharp
private readonly Dictionary<int, EventActionGroup> _groupMap;
private readonly object _gate = new();
private readonly HashSet<Task> _inFlight = new();
private readonly CancellationTokenSource _shutdownCts = new();
private readonly GroupLaneScheduler _lane;

private int _shuttingDown;
private bool _disposed;
```

| Field | Job |
|---|---|
| `_groupMap` | Id → group (immutable membership after ctor for normal use) |
| `_gate` | Shared lock object; **passed into** `GroupLaneScheduler` |
| `_inFlight` | Tasks still running; drained on shutdown |
| `_shutdownCts` | Linked into exclusive acquires so waiters cancel on shutdown |
| `_lane` | Exclusive FIFO scheduler |
| `_shuttingDown` | 0/1 flag for fast reject of new work |
| `_disposed` | Prevents tracking after dispose |

**Why share `_gate` with the lane?** Acquire/release decisions and in-flight bookkeeping must not race. One monitor keeps “am I disposed?” and “who is active on the lane?” coherent.

## Constructor

```csharp
public ActionsHandler(Dictionary<int, EventActionGroup> groupMap)
{
    ArgumentNullException.ThrowIfNull(groupMap);
    _groupMap = groupMap;
    _lane = new GroupLaneScheduler(_gate);
}
```

Null map is programmer error. The lane receives the **same** gate instance.

## `IsShuttingDown`

```csharp
public bool IsShuttingDown => Volatile.Read(ref _shuttingDown) != 0;
```

Lock-free read for the hot reject path in `ScheduleTask`. Set via `Interlocked.Exchange` in shutdown/dispose.

## `ShutdownAsync` — preferred stop

```csharp
public async Task ShutdownAsync(TimeSpan? drainTimeout = null)
{
    if (Interlocked.Exchange(ref _shuttingDown, 1) == 0)
    {
        try { _shutdownCts.Cancel(); }
        catch (ObjectDisposedException) { }
    }
    // ... snapshot in-flight, wait, then Dispose()
}
```

### Step by step

1. **First caller wins the cancel.** `Interlocked.Exchange` sets the flag; only the transition `0 → 1` cancels `_shutdownCts`. Later callers still participate in drain.
2. **Cancel may throw if already disposed** — swallowed. Shutdown must be idempotent-ish.
3. **Snapshot `_inFlight` under `_gate`** — then wait **outside** the lock (never `await` while holding `_gate`).
4. **Drain policy:**
   - With finite timeout: `WhenAny(all, Delay)`. On timeout, log via `Debug.WriteLine` and dispose anyway.
   - Without timeout: `await WhenAll`, swallow exceptions (callers already observe their own tasks).
5. **Always `Dispose()`** at the end — marks disposed, cancels waiters, disposes CTS.

**Why it matters:** host stop needs “best effort drain” without hanging forever on a task that ignores cancellation.

## `ScheduleTask` / `ScheduleTask<T>`

```csharp
public Task ScheduleTask(Func<Task> taskFunc, int identifier, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(taskFunc);
    if (IsShuttingDown)
        return Task.FromException(new InvalidOperationException("Scheduler is shutting down."));

    SchedulingNest.ThrowIfActive(this);
    return TrackInFlight(ScheduleCoreAsync(taskFunc, ResolveGroup(identifier), cancellationToken));
}
```

Order of checks is deliberate:

1. Null work → argument exception.
2. Shutting down → returns a **faulted** `Task` via `Task.FromException` (callers see the error on await; no sync throw from `ScheduleTask`).
3. Nest → **throws synchronously** if same handler is already active on this async flow.
4. Resolve group + start core + track.

The generic overload mirrors this for `Task<T>`.

**What breaks if nest check moves after acquire?** You could queue nested work onto the lane and deadlock yourself (serial group waiting for itself).

## `ScheduleCoreAsync` — the policy branch

```csharp
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
```

### FullyConcurrent path

No acquire/release. Still runs inside `RunWithNestAsync` so nest tracking applies.

### Exclusive path

1. **Link caller token + shutdown token** — cancel either waiting or (depending on user code) running work’s wait.
2. **`AcquireAsync`** — may wait FIFO.
3. **`try/finally Release`** — **always** release, even if work throws. Forgetting `finally` would freeze the lane.

Generic `ScheduleCoreAsync<T>` is the same with a return value.

## `ResolveGroup`

```csharp
if (!_groupMap.TryGetValue(identifier, out var group))
    throw new ArgumentException($"No task group found for identifier: {identifier}", nameof(identifier));
```

Unknown ids are programming/weave bugs. Fail loud.

## `RunWithNestAsync`

```csharp
using (SchedulingNest.Enter(this))
    await taskFunc().ConfigureAwait(false);
```

Push handler onto `AsyncLocal` stack for the duration of the user work, then pop on dispose of `NestScope`. Nested scheduled calls see `ThrowIfActive`.

## `TrackInFlight`

```csharp
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
```

| Detail | Why |
|---|---|
| Add under lock | HashSet is not thread-safe |
| Throw if disposed | No new tracking after teardown |
| `ContinueWith` remove | Drain set stays accurate |
| `ExecuteSynchronously` | Cheap removal often on completing thread |
| `CancellationToken.None` | Removal must run even if caller token canceled |
| Return original `task` | Callers await the real work, not a wrapper |

Generic overload casts to non-generic track and returns `Task<T>`.

## `Dispose`

```csharp
public void Dispose()
{
    lock (_gate)
    {
        if (_disposed) return;
        _disposed = true;
        _lane.MarkDisposed();
    }

    Interlocked.Exchange(ref _shuttingDown, 1);
    try { _shutdownCts.Cancel(); } catch (ObjectDisposedException) { }
    _shutdownCts.Dispose();
    _lane.CancelAllWaiters();
}
```

- Mark disposed + lane disposed under lock.
- Set shutting down, cancel + dispose CTS.
- Cancel all lane waiters so `AcquireAsync` does not hang.

Prefer `ShutdownAsync` for graceful drain; `Dispose` is the hard stop edge.

## What breaks if…

| Change | Failure |
|---|---|
| Skip `finally` Release | Exclusive lane deadlocks |
| Await while holding `_gate` | Deadlocks with lane / continuations |
| No in-flight tracking | Shutdown returns while work still runs unnoticed |
| Nest check removed | Same-handler nested serial work can self-deadlock |
| FullyConcurrent still acquires lane | Concurrent work blocks behind exclusive groups |

## See also

- Next chapter: `GroupLaneScheduler` (acquire/release details)
- Nest chapter (later): `SchedulingNest.cs`
- Tests: `GroupedScheduling.Tests/Engine/ActionsHandlerTests.cs`, shutdown tests
