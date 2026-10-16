# File: `SchedulingNest.cs`

**Path:** `GroupedScheduling/SchedulingNest.cs`  
**Visibility:** `public`, but `[EditorBrowsable(Never)]` — weaver / runtime plumbing, not IntelliSense authoring API.

## Why this file exists

Exclusive groups can **deadlock themselves** if a running scheduled method awaits another scheduled method on the **same** `ActionsHandler` (classic “hold the lane, wait for the lane”). Nest tracking forbids that pattern while still allowing **cross-handler** calls (`A → B`).

Mechanism: an `AsyncLocal` stack of handlers active on the current async flow.

## Full source (reference)

```csharp
using System.ComponentModel;

namespace GroupedScheduling;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class SchedulingNest
{
    private static readonly AsyncLocal<NestStack?> s_stack = new();

    public static bool IsActive(ActionsHandler handler) { ... }
    public static void ThrowIfActive(ActionsHandler handler) { ... }
    public static NestScope Enter(ActionsHandler handler) { ... }

    public readonly struct NestScope : IDisposable { ... }
    internal sealed class NestStack { ... }
}
```

(Complete file is short — ~85 lines. Walkthrough below covers every behavioral statement.)

## Concepts you need first

| Concept | Role here |
|---|---|
| `AsyncLocal<T>` | Value flows with async execution context across `await` / threads |
| Immutable stack via linked nodes | Push/pop without mutating shared lists across flows |
| `ReferenceEquals` | Handlers compared by instance identity, not “same group name” |
| Sync throw vs async fault | Nest violations throw when scheduling is attempted |

**Concept — why not `ThreadLocal`?** After `await`, continuation may run on another thread-pool thread. `AsyncLocal` follows the logical call chain; `ThreadLocal` would lose the nest frame.

## Who calls whom

```text
  ActionsHandler.ScheduleTask*
        |
        +--> SchedulingNest.ThrowIfActive(this)   // before queueing
        |
        v
  ScheduleCoreAsync -> RunWithNestAsync
        |
        +--> using (SchedulingNest.Enter(this))
                 await userWork()
```

Check **before** acquire; enter **around** the body. That ordering prevents “I’m waiting on the lane while already holding it via nest.”

## Field

```csharp
private static readonly AsyncLocal<NestStack?> s_stack = new();
```

One ambient stack per async flow. `null` means “no scheduled body active on this flow.”

## `IsActive`

```csharp
public static bool IsActive(ActionsHandler handler)
{
    ArgumentNullException.ThrowIfNull(handler);
    return s_stack.Value?.Contains(handler) == true;
}
```

Walks the linked stack for a reference match. Used by `ThrowIfActive` and available for diagnostics/tests.

## `ThrowIfActive`

```csharp
public static void ThrowIfActive(ActionsHandler handler)
{
    if (IsActive(handler))
    {
        throw new InvalidOperationException(
            "Nested scheduled call on the same ActionsHandler is not allowed. " +
            "Cross-instance calls (A→B) are fine; same-instance re-entry is not.");
    }
}
```

Message is intentional teaching text for app authors who hit the sharp edge at runtime. Analyzer **TSCH001** catches many static cases; **runtime is authoritative** for reflection / dynamic paths.

## `Enter`

```csharp
public static NestScope Enter(ActionsHandler handler)
{
    ArgumentNullException.ThrowIfNull(handler);
    ThrowIfActive(handler);

    var next = NestStack.Push(s_stack.Value, handler);
    s_stack.Value = next;
    return new NestScope(next);
}
```

1. Null guard.
2. Double-check nest (defense if someone calls `Enter` without `ThrowIfActive` first).
3. Push a new immutable frame.
4. Publish it to `AsyncLocal`.
5. Return a scope that pops **only if** it still owns the top.

**Why push even though `ScheduleTask` already threw?** `Enter` is the authoritative “I am now running.” The pre-check in `ScheduleTask` fails fast before lane work; `Enter` keeps the stack honest for the body duration.

## `NestScope`

```csharp
public readonly struct NestScope : IDisposable
{
    private readonly NestStack _frame;

    internal NestScope(NestStack frame) => _frame = frame;

    public void Dispose()
    {
        if (!ReferenceEquals(s_stack.Value, _frame))
            return;

        s_stack.Value = _frame.Previous;
    }
}
```

| Detail | Why |
|---|---|
| `readonly struct` | Cheap `using` scope; no heap for the scope itself |
| Pop only if still top | Avoid corrupting the stack if dispose order is wrong or a nested misuse already replaced the top |
| Restore `Previous` | Immutable stack pop |

**What breaks if Dispose always forced `Previous` without the top check?** A mismatched dispose could drop another frame’s handler and allow illegal re-entry (or hide a bug).

## `NestStack`

```csharp
internal sealed class NestStack
{
    public ActionsHandler Handler { get; }
    public NestStack? Previous { get; }

    public static NestStack Push(NestStack? previous, ActionsHandler handler)
        => new(handler, previous);

    public bool Contains(ActionsHandler handler)
    {
        for (var node = this; node is not null; node = node.Previous)
        {
            if (ReferenceEquals(node.Handler, handler))
                return true;
        }
        return false;
    }
}
```

Linked list from top → older frames. `Contains` is O(depth). Nest depth is tiny in practice (usually 0–2), so a list is fine.

## Picture

![Nest stack](../assets/04-nest-stack.png)

## Allowed vs forbidden (truth table)

| Pattern | Result | Why |
|---|---|---|
| Same instance method A awaits same instance method B (both scheduled) | **Throws** | Same handler on stack |
| Object `a` awaits object `b` | **Allowed** | Different handler instances |
| `a → b → a` | **Throws** | `a` still on stack when re-entered |
| Static scheduled → instance scheduled on same type | **Allowed** | Different handlers (static state vs instance state) |
| Non-scheduled helper called from scheduled method | **Allowed** | Helper does not call `ScheduleTask` |

## Worked timeline

```text
  await a.SaveAsync()          // handler Ha
    ThrowIfActive(Ha) OK
    Enter(Ha)  stack: [Ha]
      await b.SaveAsync()      // handler Hb
        ThrowIfActive(Hb) OK   // Ha != Hb
        Enter(Hb) stack: [Hb, Ha]
          ... body ...
        Dispose -> [Ha]
      await a.OtherAsync()     // Ha again
        ThrowIfActive(Ha) FAIL
```

## What breaks if…

| Change | Failure |
|---|---|
| Use `ThreadLocal` instead of `AsyncLocal` | Nest “forgets” across await; illegal nests slip through or false positives |
| Only check nest, never `Enter` | Nested calls after first await may not see the parent |
| Compare handlers by group name | Different objects sharing a name string would falsely conflict |
| Remove nest entirely | ExclusiveSerial self-deadlocks become possible |

## See also

- Short doc: `docs/nesting.md`
- Sample: `NestRejectSmoke`, `CrossInstanceNestSmoke`
- Tests: `Engine/NestingTests.cs`, `NestCancelCoverageTests.cs`
- Analyzer TSCH001 (static approximation)
