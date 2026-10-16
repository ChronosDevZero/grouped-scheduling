# File: `SchedulingOwnerState.cs`

**Path:** `GroupedScheduling/SchedulingOwnerState.cs`  
**Visibility:** `public` abstract + `[EditorBrowsable(Never)]`

## Why this file exists

Every woven “owner” (one object, or one static type) needs:

1. An `ActionsHandler` built from that owner’s group map.
2. An **owner-level** `CancellationTokenSource` canceled on dispose/shutdown.
3. A uniform `ShutdownAsync` / `Dispose` story.

`InstanceSchedulingState` and `StaticSchedulingState` are thin subclasses that rename the token for clarity (`ObjectToken` vs `TypeToken`). Shared logic lives here.

## Full source

```csharp
using System.ComponentModel;

namespace GroupedScheduling;

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

        try { _ownerCts.Cancel(); }
        catch (ObjectDisposedException) { }

        await Handler.ShutdownAsync(drainTimeout).ConfigureAwait(false);
        _ownerCts.Dispose();
    }

    public void Dispose() => Shutdown();
}
```

## Concepts you need first

| Concept | Role |
|---|---|
| One handler per owner | Instance vs static maps are separate owners |
| Owner CTS vs handler `_shutdownCts` | Owner cancel feeds linked call tokens; handler shutdown cancels lane waiters + drains |
| Idempotent shutdown | Double dispose / host stop + dispose must not throw chaos |
| Sync-over-async `Shutdown()` | `Dispose()` path; prefer `ShutdownAsync` when you can await |

## Who creates these

```text
  Weaver-injected factory methods
        |
        v
  HandlerRegistry.GetOrCreateInstanceState / GetOrCreateStaticState
        |
        v
  new InstanceSchedulingState(map)  or  new StaticSchedulingState(map)
        |
        v
  base ctor -> new ActionsHandler(map)
```

## Fields and properties

```csharp
private readonly CancellationTokenSource _ownerCts = new();
private int _disposed;
public ActionsHandler Handler { get; }
protected CancellationToken OwnerToken => _ownerCts.Token;
```

| Member | Job |
|---|---|
| `_ownerCts` | Canceled when this owner shuts down |
| `_disposed` | 0/1 guard for single shutdown |
| `Handler` | The scheduler for this owner’s groups |
| `OwnerToken` | Exposed to subclasses as Object/Type token |

## Constructor

```csharp
protected SchedulingOwnerState(Dictionary<int, EventActionGroup> groupMap)
{
    Handler = new ActionsHandler(groupMap);
}
```

Each owner gets a **fresh** handler. Two instances of `OrdersService` → two handlers → two independent `"orders"` lanes.

## `CreateCallToken`

```csharp
public CancellationTokenSource CreateCallToken(CancellationToken methodToken = default)
    => SchedulingRuntime.CreateLinkedCallToken(OwnerToken, methodToken);
```

Convenience for weave hooks. Caller must dispose the returned CTS (hooks do this in `finally`).

## `ShutdownAsync`

```csharp
if (Interlocked.Exchange(ref _disposed, 1) != 0)
    return;
```

First caller performs shutdown; later callers no-op. Important when DI dispose races host `ShutdownAllAsync`.

```csharp
try { _ownerCts.Cancel(); }
catch (ObjectDisposedException) { }

await Handler.ShutdownAsync(drainTimeout).ConfigureAwait(false);
_ownerCts.Dispose();
```

Order:

1. Cancel owner token → in-flight bodies using linked tokens observe cancel.
2. Handler shutdown → reject new work, cancel exclusive waiters, drain in-flight, dispose handler.
3. Dispose owner CTS.

**Why cancel owner before handler drain?** So cooperative work can finish early during the drain window instead of only being abandoned at dispose.

## `Dispose` / `Shutdown`

```csharp
public void Dispose() => Shutdown();
public void Shutdown() => ShutdownAsync().GetAwaiter().GetResult();
```

Weaver injects `IDisposable` on instance types; DI calls `Dispose`. Blocking wait is a compromise for the sync dispose contract. Host stop uses the async registry path instead.

**What breaks if Dispose only canceled the CTS and skipped `Handler.ShutdownAsync`?** Lane waiters and in-flight tracking would not drain; new schedules might still race.

## Layered cancellation (remember this diagram)

```text
  methodToken (caller)
        \
         +--> linked call token --> user body (if method has CT)
        /
  OwnerToken (_ownerCts)

  Handler._shutdownCts --> linked into exclusive AcquireAsync waits
```

Two different “shutdown” tokens exist on purpose:

- **Owner** — cancels work that received a linked call token.
- **Handler** — cancels waiters sitting in `GroupLaneScheduler.AcquireAsync`.

## What breaks if…

| Change | Failure |
|---|---|
| Share one handler across all instances | Groups become process-wide accidentally |
| Skip `Interlocked` dispose guard | Double dispose of CTS / double handler shutdown races |
| Dispose owner CTS before handler shutdown | Possible odd ObjectDisposed during cancel ordering |

## See also

- Subclasses next
- `docs/shutdown.md`
- Hosted service drain timeout (30s)
