# File: `ScheduleMode.cs`

**Path:** `GroupedScheduling/ScheduleMode.cs`

## Why this file exists

`ScheduleMode` is the **concurrency policy** attached to a named group. The attribute stores it; `EventActionGroup` turns it into booleans the lane understands; the weaver forbids conflicting modes for the same name on one type.

## Full source

```csharp
namespace GroupedScheduling;

/// <summary>
/// Concurrency policy for a schedule group.
/// </summary>
public enum ScheduleMode
{
    /// <summary>
    /// Bypass the exclusive group queue; run immediately in parallel with all other work.
    /// </summary>
    FullyConcurrent = 0,

    /// <summary>
    /// Only one group is active at a time; tasks inside the group run one after another.
    /// Waiting groups are activated in FIFO order.
    /// </summary>
    ExclusiveSerial = 1,

    /// <summary>
    /// Only one group is active at a time; tasks inside the group may run in parallel.
    /// Waiting groups are activated in FIFO order.
    /// </summary>
    ExclusiveParallel = 2,
}
```

## Concepts you need first

**Two axes of concurrency** (do not collapse them mentally):

1. **Across groups** — may group `"orders"` and `"reports"` overlap?
2. **Inside a group** — may two `"orders"` tasks overlap?

| Mode | Across exclusive groups | Inside group |
|---|---|---|
| `ExclusiveSerial` | No overlap (one active exclusive group) | No overlap |
| `ExclusiveParallel` | No overlap (one active exclusive group) | Overlap OK |
| `FullyConcurrent` | Does not take the exclusive lane | Overlap OK with everything |

`FullyConcurrent` is not “serial inside, concurrent outside.” It **skips the exclusive lane entirely**.

## Line walkthrough

### Why explicit numeric values?

```csharp
FullyConcurrent = 0,
ExclusiveSerial = 1,
ExclusiveParallel = 2,
```

Stable ordinals matter when metadata is persisted or compared across tools. Defaulting the first enumerator to 0 is idiomatic; calling out values documents that they are part of the contract.

### `FullyConcurrent = 0`

“Run now; do not wait for the exclusive lane.” Still subject to:

- Nest rules (same-handler re-entry forbidden).
- Shutdown checks (`IsShuttingDown`).
- In-flight tracking.

**Why it matters:** background-friendly work can proceed while exclusive groups drain, without joining their FIFO queue.

### `ExclusiveSerial = 1`

Classic mutex-per-group-with-global-exclusive-lane behavior:

- Only one exclusive group is active on that handler.
- Within the active group, `Running` stays at most 1 (lane enforces).

Use when methods mutate shared state that cannot overlap.

### `ExclusiveParallel = 2`

Same exclusive lane as serial, but once a group is active, multiple tasks in that group may run together (`CanRunConcurrentlyInGroup`).

Use when the *category* of work must exclude other categories, but peers inside the category are safe to overlap.

## Mapping into runtime flags

`EventActionGroup` exposes:

```text
CanRunConcurrently          <=>  Mode == FullyConcurrent
CanRunConcurrentlyInGroup   <=>  Mode == ExclusiveParallel
```

`ActionsHandler.ScheduleCoreAsync` branches on `CanRunConcurrently`. The lane’s `TryEnterNow_NoLock` / `GrantWaiters_NoLock` branch on `CanRunConcurrentlyInGroup`.

## Same name, different modes

On one declaring type, all methods that share a group **name** must use the **same** mode. The weaver throws (build failure) on conflict. That prevents one method promising serial safety while another in the same named group runs parallel.

## What breaks if…

| Change | Failure |
|---|---|
| Treat `FullyConcurrent` like exclusive with “infinite parallelism” inside the lane | Concurrent work would still queue behind exclusive groups — wrong product behavior |
| Collapse serial/parallel into one mode | Lose the in-group parallelism knob |
| Reorder enum values without care | Risk breaking any persisted ordinals / snapshots |

## See also

- `docs/schedule-modes.md`
- Sample: `samples/.../Workers/ModesDemoWorker.cs`
- Tests: engine / worker behavior around modes
