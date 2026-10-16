# File: `EventActionGroup.cs`

**Path:** `GroupedScheduling/EventActionGroup.cs`

## Why this file exists

The attribute is **compile-time metadata**. At runtime, the handler needs a small object that means “this named group with this mode,” plus cheap booleans for hot paths. `EventActionGroup` is that object.

You can also construct these manually when using `ActionsHandler` without weaving (tests / advanced scenarios).

## Full source

```csharp
namespace GroupedScheduling;

/// <summary>
/// Named work group with a <see cref="ScheduleMode"/> concurrency policy.
/// </summary>
public sealed class EventActionGroup
{
    public string Name { get; }
    public ScheduleMode Mode { get; }

    public bool CanRunConcurrently => Mode == ScheduleMode.FullyConcurrent;
    public bool CanRunConcurrentlyInGroup => Mode == ScheduleMode.ExclusiveParallel;

    public EventActionGroup(string name, ScheduleMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Mode = mode;
    }
}
```

## Who calls whom

```text
  Weaver / test setup
        |
        v
  new EventActionGroup(name, mode)
        |
        v
  Dictionary<int, EventActionGroup>   // group id -> group
        |
        v
  ActionsHandler / GroupLaneScheduler
```

Integer **identifiers** are stable keys the weaver emits into IL. The dictionary maps those ids to `EventActionGroup` instances.

## Line walkthrough

### Identity properties

```csharp
public string Name { get; }
public ScheduleMode Mode { get; }
```

Name is for humans and diagnostics; mode is policy. Equality is **reference-based** for lane maps (`Dictionary<EventActionGroup, …>` and `ReferenceEquals` checks in the lane). The weaver/runtime should reuse the same instance per id in a handler’s map — not allocate a new `EventActionGroup` per call with the same name.

**Concept:** the lane’s `_activeGroup` and `_waitingGroups` compare by reference. Duplicate instances with the same name would be treated as different groups.

### Hot-path booleans

```csharp
public bool CanRunConcurrently => Mode == ScheduleMode.FullyConcurrent;
public bool CanRunConcurrentlyInGroup => Mode == ScheduleMode.ExclusiveParallel;
```

These avoid scattering `switch (mode)` across handler and lane. Read them as:

- **CanRunConcurrently** — “skip exclusive lane.”
- **CanRunConcurrentlyInGroup** — “while this group is active, allow `Running > 1`.”

ExclusiveSerial yields **false / false**.

### Constructor validation

Same whitespace rule as the attribute. Runtime construction (tests) gets the same guard as attribute construction.

## What breaks if…

| Change | Failure |
|---|---|
| New `EventActionGroup` per schedule call with same name | Lane treats them as distinct; FIFO / active group logic corrupts |
| Make equality by name | Might seem nice, but then mutable mode or duplicates become subtle; current design is “interned by map” |

## See also

- Manual scheduling in tests via `ActionsHandler`
- Weaver group map generation (Part III)
