# File: `SchedulingModuleWeaver.Dispose.cs`

**Path:** `GroupedScheduling.Weaver/SchedulingModuleWeaver.Dispose.cs`

## Why this file exists

Instance scheduling owns an `InstanceSchedulingState` that must shut down when the object is disposed. The weaver ensures the type implements `IDisposable` and that `Dispose` **first** tears down scheduling state, then runs any prior/base dispose logic.

**Static** handlers are **not** disposed via type `IDisposable` — process stop walks `HandlerRegistry`.

## `EnsureDispose` algorithm

```text
  ensure type (or ancestor) lists IDisposable
  find existing Dispose() on this type
  find base Dispose() (public/family)

  if no existing Dispose on this type:
      emit new Dispose:
        DisposeInstanceState(field)
        [call base.Dispose if any]
        ret
  else:
      rename existing -> __Dispose_Original (private, non-virtual)
      emit new public Dispose stub:
        DisposeInstanceState(field)
        call __Dispose_Original
        ret
```

## Why scheduling dispose runs first

```csharp
il.Emit(OpCodes.Ldarg_0);
il.Emit(OpCodes.Ldfld, instanceField);
il.Emit(OpCodes.Call, disposeHook);  // SchedulingWeaveHooks.DisposeInstanceState
// then user/base dispose
```

Cancel owner token and drain the handler **before** user dispose logic runs further teardown that scheduled methods might still need. Also matches “stop accepting work early.”

## Interface injection

```csharp
if (!TypeOrAncestorDeclaresIDisposable(type))
    type.Interfaces.Add(new InterfaceImplementation(idisposable));
```

If a base already implements `IDisposable`, do not re-add; still override/wrap `Dispose` as needed.

## Virtual / NewSlot / Final flags

When creating a brand-new `Dispose`:

- If no base dispose → `NewSlot | Final` (introduce the slot cleanly).
- If base dispose exists → virtual override without Final.

When wrapping an existing dispose, the stub preserves NewSlot/Final intent carefully so the vtable slot stays correct.

## What breaks if…

| Change | Failure |
|---|---|
| Call user dispose before scheduling dispose | Work may still schedule during teardown |
| Dispose static state in instance Dispose | Wrong lifetime; statics are process-scoped |
| Break vtable flags | `Dispose` not called via interface cast |

## See also

- Runtime: `SchedulingOwnerState.Dispose`
- Hosting: registry shutdown vs DI dispose ordering
