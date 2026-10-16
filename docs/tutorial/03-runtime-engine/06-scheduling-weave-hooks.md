# File: `SchedulingWeaveHooks.cs`

**Path:** `GroupedScheduling/SchedulingWeaveHooks.cs`  
**Visibility:** `public` + `[EditorBrowsable(Never)]`

## Why this file exists

The IL weaver must call **stable, named entry points**. If those signatures move, woven assemblies break. This static class is the **binary contract** between:

- `GroupedScheduling.Weaver` (emits calls by metadata name), and
- `GroupedScheduling` runtime (implements scheduling).

App authors should not call these APIs — they call normal methods; the weaver inserts the hooks.

## Stability constants

```csharp
public const string InstanceStateFieldName = "__groupedSchedulingState";
public const string StaticStateFieldName = "__groupedSchedulingStaticState";
public const string CreateInstanceStateMethodName = "__GS_CreateInstanceState";
public const string CreateStaticStateMethodName = "__GS_CreateStaticState";
```

| Constant | Meaning |
|---|---|
| Field names | Injected fields on woven types |
| Create* method names | Injected factories the weaver emits / binds |

**What breaks if you rename these without updating the weaver?** Weave fails or emits broken IL. Treat renames as a **paired change** across runtime + weaver (+ tests).

## Concepts you need first

| Concept | Role |
|---|---|
| Overload matrix | Instance/static × Task/ValueTask × with/without CT × T/non-T |
| `Func<Task>` wrappers | Capture args; delay body until lane grants |
| Linked CTS lifetime | Create before schedule; dispose in `finally` after await |
| `ValueTask` → `AsTask()` | Handler tracks `Task`; ValueTask is adapted at the edge |

## Who calls whom

```text
  Woven public method
        |
        v
  InitInstanceState / static field ensure
        |
        v
  SchedulingWeaveHooks.Schedule* / ScheduleStatic*
        |
        +-- no CT --> Handler.ScheduleTask(work, id, OwnerToken)
        |
        +-- with CT --> CreateCallToken -> RunWithLinkedToken
                              |
                              v
                       Handler.ScheduleTask(() => work(linked.Token), id, linked.Token)
```

## `InitInstanceState`

```csharp
public static InstanceSchedulingState InitInstanceState(object owner, Func<InstanceSchedulingState> factory)
    => HandlerRegistry.GetOrCreateInstanceState(owner, factory);
```

Thin alias so woven IL has one obvious call. Ensures registry attachment + returns the state stored in the instance field.

## Overload families (map of the file)

Think in four axes; almost every combination exists:

1. **Owner:** instance (`Schedule`) vs static (`ScheduleStatic`)
2. **Return:** `Task` / `Task<T>` / `ValueTask` / `ValueTask<T>`
3. **Token:** work is `Func<Task>` vs `Func<CancellationToken, Task>` (+ ValueTask variants)
4. **Generic:** non-generic vs `T`

If you understand **one** path deeply, the rest are mechanical copies.

## Path A — instance, Task, no method CT

```csharp
public static Task Schedule(
    InstanceSchedulingState state,
    int groupId,
    Func<Task> work)
    => state.Handler.ScheduleTask(work, groupId, state.ObjectToken);
```

- `groupId` — integer key into the owner’s group map (weaver assigns stable ids).
- `work` — closure over the original body / args.
- Passes **object token** so owner dispose can cancel waits; body itself may not observe a token if the original method had none.

Generic twin returns `Task<T>` the same way.

## Path B — instance, Task, with method CT

```csharp
public static Task Schedule(
    InstanceSchedulingState state,
    int groupId,
    Func<CancellationToken, Task> work,
    CancellationToken methodToken)
{
    var linked = state.CreateCallToken(methodToken);
    return RunWithLinkedToken(state.Handler, groupId, linked, work);
}
```

Creates linked CTS, then delegates to the private runner that **owns disposal**.

### `RunWithLinkedToken` (Task)

```csharp
private static async Task RunWithLinkedToken(
    ActionsHandler handler,
    int groupId,
    CancellationTokenSource linked,
    Func<CancellationToken, Task> work)
{
    try
    {
        await handler.ScheduleTask(() => work(linked.Token), groupId, linked.Token).ConfigureAwait(false);
    }
    finally
    {
        linked.Dispose();
    }
}
```

| Detail | Why |
|---|---|
| `() => work(linked.Token)` | Body receives the **linked** token, not only the caller token |
| Same token passed to `ScheduleTask` | Exclusive acquire waits cancel on owner/caller cancel |
| `finally Dispose` | Linked CTS registrations freed even when work throws |
| `async` helper | Needed for `try/finally` around `await` |

Generic `RunWithLinkedToken<T>` returns the value the same way.

## Path C — static variants

`ScheduleStatic` mirrors Path A/B but uses `StaticSchedulingState` and `TypeToken`:

```csharp
public static Task ScheduleStatic(
    StaticSchedulingState state,
    int groupId,
    Func<Task> work)
    => state.Handler.ScheduleTask(work, groupId, state.TypeToken);
```

Static + CT overloads call the **same** `RunWithLinkedToken` helpers — disposal logic is not duplicated per owner kind.

## Path D — ValueTask adapters

```csharp
public static ValueTask Schedule(
    InstanceSchedulingState state,
    int groupId,
    Func<ValueTask> work)
    => new(state.Handler.ScheduleTask(() => work().AsTask(), groupId, state.ObjectToken));
```

**Why `AsTask()`?** `ActionsHandler` tracks and schedules `Task` / `Task<T>`. ValueTask is a return-type façade for callers; internally the lane still sees a `Task`.

```csharp
public static ValueTask Schedule(
    InstanceSchedulingState state,
    int groupId,
    Func<CancellationToken, ValueTask> work,
    CancellationToken methodToken)
{
    var linked = state.CreateCallToken(methodToken);
    return new ValueTask(RunWithLinkedToken(state.Handler, groupId, linked, work));
}
```

There are dedicated `RunWithLinkedToken` overloads that accept `Func<CancellationToken, ValueTask>` and call `.AsTask()` inside `ScheduleTask`.

Wrapping `new ValueTask(task)` preserves the public `ValueTask` signature of the woven method.

## `DisposeInstanceState`

```csharp
public static void DisposeInstanceState(InstanceSchedulingState? state)
    => state?.Dispose();
```

Emitted from woven `Dispose` paths. Null-safe for partially initialized objects.

## End-to-end: method with CT (conceptual)

You write:

```csharp
[ScheduleGroup("orders", ScheduleMode.ExclusiveSerial)]
public async Task SaveAsync(Order o, CancellationToken ct) { ... }
```

Woven shape (conceptual C#, not literal IL):

```csharp
public Task SaveAsync(Order o, CancellationToken ct)
{
    var state = EnsureInstanceState(); // field + InitInstanceState
    return SchedulingWeaveHooks.Schedule(
        state,
        groupId: ORDERS,
        work: linked => __SaveAsync_Impl(o, linked),
        methodToken: ct);
}
```

Inside hooks: link tokens → `ActionsHandler.ScheduleTask` → nest + lane → your impl with linked token → dispose CTS.

## End-to-end: method without CT

```csharp
Schedule(state, groupId, () => __Impl(args));
// uses ObjectToken/TypeToken for ScheduleTask's cancellationToken parameter
// body does not receive a shutdown token
```

Queued exclusive waits can still cancel; the body cannot observe cancel unless you add a `CancellationToken` parameter (TSCH005 nudges this).

## What breaks if…

| Change | Failure |
|---|---|
| Rename method without weaver update | MissingMethod / weave bind failure |
| Dispose linked CTS before await completes | Token disposed while waiters still registered |
| Forget `.AsTask()` for ValueTask work | Cannot schedule/track through handler |
| Pass `methodToken` only (skip owner link) | Object dispose would not cancel that call |
| Pass owner token only when method has CT | Caller cancel ignored |

## See also

- Part III weaver — who emits these calls
- `docs/signatures.md` — supported signatures
- Tests: weave coverage + ValueTask sample smokes
