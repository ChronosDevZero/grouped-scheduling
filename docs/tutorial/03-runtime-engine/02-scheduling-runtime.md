# File: `SchedulingRuntime.cs`

**Path:** `GroupedScheduling/SchedulingRuntime.cs`  
**Visibility:** `public` + `[EditorBrowsable(Never)]`

## Why this file exists

Tiny helper used when a woven call (or owner state) needs a **linked** `CancellationTokenSource` from:

- the **owner** token (object CTS or type CTS), and
- an optional **method** token (caller-supplied `CancellationToken`).

Keeping the branching in one place avoids subtle bugs duplicated across weave hooks.

## Full source

```csharp
using System.ComponentModel;

namespace GroupedScheduling;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class SchedulingRuntime
{
    /// <summary>
    /// Links an object-level shutdown token with an optional per-call token.
    /// Dispose the returned <see cref="CancellationTokenSource"/> when the call completes.
    /// </summary>
    public static CancellationTokenSource CreateLinkedCallToken(
        CancellationToken objectToken,
        CancellationToken methodToken = default)
    {
        if (methodToken.CanBeCanceled)
            return CancellationTokenSource.CreateLinkedTokenSource(objectToken, methodToken);

        if (objectToken.CanBeCanceled)
            return CancellationTokenSource.CreateLinkedTokenSource(objectToken);

        return new CancellationTokenSource();
    }
}
```

## Concepts you need first

| Concept | Role |
|---|---|
| `CancellationToken.CanBeCanceled` | `default` tokens and `None` are not cancelable |
| Linked CTS | Cancels when **any** linked source cancels |
| Dispose linked CTS | Avoids leaking registrations; hooks dispose in `finally` |

## Line walkthrough

### Case 1 — method token can cancel

```csharp
if (methodToken.CanBeCanceled)
    return CancellationTokenSource.CreateLinkedTokenSource(objectToken, methodToken);
```

Caller cancel **or** owner shutdown cancel the linked token. This is the common path for methods that take `CancellationToken`.

Note: even if `objectToken` is non-cancelable, linking still works; the useful cancel source is the method token. If both can cancel, either wins.

### Case 2 — only owner can cancel

```csharp
if (objectToken.CanBeCanceled)
    return CancellationTokenSource.CreateLinkedTokenSource(objectToken);
```

Method had no usable token (or passed `default`). Owner dispose / registry shutdown can still cancel in-flight work that observes this token — **but only if the weave path actually passes the linked token into the body**. Methods without a `CancellationToken` parameter do not get a shutdown token into user code (see shutdown docs / TSCH005).

### Case 3 — neither can cancel

```csharp
return new CancellationTokenSource();
```

Returns a fresh CTS whose token is not canceled by anyone yet. Callers still must dispose it. This keeps a uniform “always get a CTS to dispose” API for weave hooks.

**Why not return `null`?** Hooks always want a CTS to dispose in `finally`. A non-null dummy simplifies IL and C# helper code.

## Who calls this

```text
  SchedulingOwnerState.CreateCallToken(methodToken)
        |
        v
  SchedulingRuntime.CreateLinkedCallToken(OwnerToken, methodToken)
        |
        v
  SchedulingWeaveHooks.Schedule*(..., methodToken)
        |
        v
  RunWithLinkedToken -> dispose CTS in finally
```

## What breaks if…

| Change | Failure |
|---|---|
| Always link both without `CanBeCanceled` checks | Usually OK, but linking `None` is unnecessary churn; the branches document intent |
| Forget dispose in hooks | Registration leaks under load |
| Use owner token alone when method token exists | Caller cancel would not reach the body |

## See also

- `docs/shutdown.md`
- Next: `SchedulingOwnerState`
- Weave hooks `RunWithLinkedToken*`
