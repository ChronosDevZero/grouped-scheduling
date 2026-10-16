# File: `SchedulingModuleWeaver.Rewrite.cs` (method rewrite)

**Path:** `GroupedScheduling.Weaver/SchedulingModuleWeaver.Rewrite.cs` (~850 lines)

This file is the heart of the weaver. You do not need to memorize every remap helper on first read — learn the **pipeline**, then zoom into generics/closures when you change them.

## Pipeline for one method

```text
  RewriteMethod(method, stateField, groupId, isStatic)
        |
        +--> IndexOfCancellationToken
        +--> CloneBodyToPrivateOriginal  --> __Name_Original
        +--> clear stub body
        +--> emit:
        |      load state (ldfld / ldsfld)
        |      ldc.i4 groupId
        |      EmitWorkDelegate  --> Func on stack
        |      [ldarg ct if any]
        |      call SchedulingWeaveHooks.Schedule*
        |      ret
        +--> EnsureDebuggerStepThrough
```

## Why keep the same `MethodDefinition`

```csharp
// Keep MethodDefinition identity for the public method so existing call sites still hit the stub.
var original = CloneBodyToPrivateOriginal(type, method);
method.Body.Instructions.Clear();
...
```

Moving IL to a new method and clearing the old body preserves the public method’s metadata token / name / signature. Call sites compiled against `SaveAsync` still call `SaveAsync`.

## `CloneBodyToPrivateOriginal`

Creates `__SaveAsync_Original` (private, static if original was static):

1. Copy method generic parameters + constraints (remapped).
2. Copy parameters with remapped types.
3. **Move** async/iterator/DebuggerStepThrough attributes onto the original (the stub is not the async state machine).
4. Copy locals (remapped) and **move** instructions (remap variable operands + type/method/field refs).
5. Move exception handlers.
6. Add method to the type.

**Concept — attribute move:** `AsyncStateMachineAttribute` must point at the method that actually contains the async machinery. That is `__Original`, not the stub.

## Stub IL (conceptual C#)

Instance method with CT:

```csharp
[DebuggerStepThrough]
public Task SaveAsync(Order o, CancellationToken ct)
{
    return SchedulingWeaveHooks.Schedule(
        this.__groupedSchedulingState,
        groupId,
        work: /* Func<CancellationToken, Task> */,
        methodToken: ct);
}
```

Without CT, the `methodToken` argument and `Func` arity change (3-arg hook).

## `EmitWorkDelegate` — two strategies

### Strategy A — no captured args (only CT or zero params)

`EmitDirectMethodGroupFunc`:

```text
  ldarg.0 / ldnull          // target for instance / static
  ldftn __Original[...]
  newobj Func<...>
```

This is a cheap method-group conversion. Works when the original’s remaining parameters are only the cancellation token (passed by the hook later) or nothing.

### Strategy B — capture business args

When there are non-CT parameters, work cannot run until the lane grants — but args must be preserved **now**. The weaver emits a nested display class:

```text
  <>c__GS_SaveAsync_<rid>
    fields: __this?, arg0, arg1, ...
    Invoke([CancellationToken]) -> calls __Original with captured args + linked token
```

Stub:

```text
  closure = new <>c__GS_...()
  closure.__this = this
  closure.arg0 = o
  ...
  newobj Func<CancellationToken, Task>(closure, ldftn Invoke)
```

**Why skip capturing the CT parameter?** The linked token is supplied later by `SchedulingWeaveHooks` into `Invoke(CancellationToken)`. Capturing the caller token in a field would ignore owner/shutdown linking.

## Closure generics (the hard part)

Display classes mirror the C# compiler’s shape:

1. Copy **declaring type** generic parameters onto the nested type.
2. Append **method** generic parameters.
3. Remap field/return types from stub GPs → closure GPs (`RemapToClosure`).
4. At the stub site, close the nested type as `Nested<!OuterT, !MethodT>` (`ClosedClosureAtStubSite`).
5. When Invoke calls `__Original`, specialize method GPs from the trailing closure GPs.

If you break this, symptoms are `BadImageFormatException`, invalid IL, or wrong type load for `Foo<T>.BarAsync<U>`.

## `ImportScheduleHook`

Reflects over `SchedulingWeaveHooks` methods named `Schedule` / `ScheduleStatic`, picks the overload by:

- arity 3 vs 4 (with token),
- whether it is open generic (`Task<T>` / `ValueTask<T>`),
- `Func` result type matching Task / ValueTask family.

Then `ImportReference` (+ `GenericInstanceMethod` for T).

**What breaks if a new awaitable return type is added to the runtime but not here?** Weave throws in `ClassifyAwaitable` or fails `Single(...)`.

## Unsupported parameters

`HasUnsupportedParameter` shares message text with analyzers via `UnsupportedParameterMessages` (linked source). Roslyn and Cecil detect separately — same user-facing reasons.

## Remap helpers (map for maintainers)

| Helper | Job |
|---|---|
| `RemapTypeToOwner` | Retarget `!T` from method A to method B |
| `RemapMethodReference` / `RemapFieldReference` | Fix operands when moving IL |
| `MethodOnClosed` / `FieldOnClosed` | MemberRef on `Foo<!0>` |
| `SpecializeMethod` | `Original<!MethodT>` at stub |
| `MakeFuncCtor` | `newobj Func(object, IntPtr)` |

You rarely call these from outside; they exist so generics do not corrupt moved IL.

## Before / after mental model (args + CT)

```text
  USER:
    Task SaveAsync(Order o, CancellationToken ct)

  AFTER:
    SaveAsync(o, ct):
      Schedule(state, id, Func(ct2 => __SaveAsync_Original(o, ct2)), ct)

    __SaveAsync_Original(o, ct):
      <original async body>
```

`o` lives in the closure; `ct2` is the linked token from hooks.

## What breaks if…

| Change | Failure |
|---|---|
| Leave async attribute on stub | Runtime / debugger confusion; wrong state machine |
| Capture CT in closure | Shutdown link bypassed |
| Forget `DebuggerStepThrough` | Painful F10 through stubs |
| Break GP remap | Generic methods fail at runtime |
| Rewrite without moving EH | try/catch in user body lost |

## See also

- Weave tests: `Tests/Weave/*`
- Sample: generics + ValueTask smokes
- Short doc: `docs/weaving.md`
