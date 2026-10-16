# Why body wrapping (not proxies / call-site rewrite)

## The product requirement

Attributed methods must stay scheduled when taken as a **method group** or invoked via **delegate** / **reflection**:

```csharp
Func<Task> f = orders.SaveAsync;   // method group
await f();                         // must still hit the lane

var mi = typeof(OrdersService).GetMethod(nameof(OrdersService.SaveAsync))!;
await (Task)mi.Invoke(orders, new object[] { order, ct })!;  // same
```

If you only rewrote **call sites** in assemblies you compile, those two paths would bypass scheduling whenever the call site was not rewritten (other assemblies, reflection, `Expression`, etc.).

## What body wrapping means

The **method definition** itself becomes a thin stub. The original IL moves to a private companion (`__SaveAsync_Original`). Every entry path that resolves to that method token hits the stub.

![Weave before / after](../assets/06-weave-before-after.png)

```text
  BEFORE WEAVE                     AFTER WEAVE
  ------------                     -----------
  SaveAsync:                       SaveAsync:          [DebuggerStepThrough]
    <user IL>                        load state
                                     load groupId
                                     build Func -> __SaveAsync_Original
                                     call SchedulingWeaveHooks.Schedule*
                                     ret

                                   __SaveAsync_Original:  (private)
                                     <user IL>
```

Public metadata identity (name, signature, token consumers care about for binding) stays on `SaveAsync`. Callers do not change.

## Alternatives considered (and rejected for v1)

| Approach | Why not (here) |
|---|---|
| Roslyn source generators rewriting call sites | Misses reflection / external callers |
| Dispatch proxies / Castle | Requires factory; method groups to real methods bypass proxy |
| Harmony / runtime patching | Fragile, not a build-time package story |
| Fody as only host | Deferred; raw Cecil MSBuild path works |

## Mental link to runtime

The stub’s only job is to call the overload matrix on `SchedulingWeaveHooks` you already studied. The weaver does not reimplement the lane in IL — it **binds** to the runtime.
