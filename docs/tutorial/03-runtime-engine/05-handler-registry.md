# File: `HandlerRegistry.cs`

**Path:** `GroupedScheduling/HandlerRegistry.cs`  
**Visibility:** `public` + `[EditorBrowsable(Never)]`

## Why this file exists

Host stop must find **every** live scheduling owner in the process:

- Instance states attached to objects (possibly many).
- Static states keyed by type.

`AddGroupedScheduling()`’s hosted service calls `ShutdownAllAsync`. The weaver never talks to the host; the registry is the indirection.

## Full source

```csharp
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GroupedScheduling;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class HandlerRegistry
{
    private static readonly ConditionalWeakTable<object, InstanceSchedulingState> s_instances = new();
    private static readonly ConcurrentDictionary<Type, StaticSchedulingState> s_statics = new();

    public static InstanceSchedulingState GetOrCreateInstanceState(
        object owner,
        Func<InstanceSchedulingState> factory) { ... }

    public static bool TryGetInstanceState(object owner, out InstanceSchedulingState? state) { ... }

    public static StaticSchedulingState GetOrCreateStaticState(
        Type ownerType,
        Func<StaticSchedulingState> factory) { ... }

    public static void ShutdownAll(TimeSpan? drainTimeout = null)
        => ShutdownAllAsync(drainTimeout).GetAwaiter().GetResult();

    public static async Task ShutdownAllAsync(TimeSpan? drainTimeout = null) { ... }
}
```

## Concepts you need first

| Concept | Role |
|---|---|
| `ConditionalWeakTable` | Attach state to an object **without** keeping it alive (no GC pin) |
| `ConcurrentDictionary` | Thread-safe static map; entries stay until shutdown |
| Process-wide singleton statics | Tests that call `ShutdownAll` can cancel **other** tests’ handlers |

**Concept — why CWT for instances?** If the registry used a strong `Dictionary<object, …>`, disposing/forgetting an object would still leave it rooted forever. CWT lets the object become collectible; the state goes away with it. **But** scheduling shutdown still must not rely on GC — dispose / host stop is required for cancel+drain.

## Fields

```csharp
private static readonly ConditionalWeakTable<object, InstanceSchedulingState> s_instances = new();
private static readonly ConcurrentDictionary<Type, StaticSchedulingState> s_statics = new();
```

Two universes, two collections.

## `GetOrCreateInstanceState`

```csharp
public static InstanceSchedulingState GetOrCreateInstanceState(
    object owner,
    Func<InstanceSchedulingState> factory)
{
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(factory);
    return s_instances.GetValue(owner, _ => factory());
}
```

`ConditionalWeakTable.GetValue` is the standard “lazy attach” API. The factory runs once per owner. Weave hooks call this from `InitInstanceState`.

**Note:** the weaver also stores the state in an instance field for fast subsequent access; the table is for **discovery at shutdown** and first-time create races.

## `TryGetInstanceState`

```csharp
public static bool TryGetInstanceState(object owner, out InstanceSchedulingState? state)
{
    ArgumentNullException.ThrowIfNull(owner);
    if (s_instances.TryGetValue(owner, out var found))
    {
        state = found;
        return true;
    }
    state = null;
    return false;
}
```

Non-creating lookup — useful for dispose paths / tests that must not allocate.

## `GetOrCreateStaticState`

```csharp
public static StaticSchedulingState GetOrCreateStaticState(Type ownerType, Func<StaticSchedulingState> factory)
{
    ArgumentNullException.ThrowIfNull(ownerType);
    ArgumentNullException.ThrowIfNull(factory);
    return s_statics.GetOrAdd(ownerType, _ => factory());
}
```

`GetOrAdd` ignores the factory’s argument type key already provided — the `_` discard is intentional. One state per `Type` key.

## `ShutdownAll` / `ShutdownAllAsync`

```csharp
public static async Task ShutdownAllAsync(TimeSpan? drainTimeout = null)
{
    var tasks = new List<Task>();
    foreach (var pair in s_statics)
        tasks.Add(pair.Value.ShutdownAsync(drainTimeout));

    foreach (var pair in s_instances)
        tasks.Add(pair.Value.ShutdownAsync(drainTimeout));

    await Task.WhenAll(tasks).ConfigureAwait(false);
}
```

### Step by step

1. Snapshot shutdown tasks for **all statics**, then **all instances**.
2. `WhenAll` — parallel best-effort drain.
3. Each owner’s `ShutdownAsync` is idempotent.

**Enumeration of `ConditionalWeakTable`:** supported for this purpose; you see currently attached values. Objects already collected are gone.

**Hosting default:** `GroupedSchedulingHostedService.StopAsync` uses `drainTimeout: TimeSpan.FromSeconds(30)`.

### Sync wrapper

```csharp
public static void ShutdownAll(TimeSpan? drainTimeout = null)
    => ShutdownAllAsync(drainTimeout).GetAwaiter().GetResult();
```

For non-async hosts / tests. Prefer async in ASP.NET / Generic Host.

## Danger zone: unit tests

**Do not** call process-wide `ShutdownAll` from parallel tests that share the registry — you will cancel other tests’ handlers. Prefer disposing the specific owner under test, or isolate with non-parallel test collections if you must call `ShutdownAll`.

## Call graph with host

![Shutdown layers](../assets/05-shutdown-layers.png)

```text
  Host stopping
      |
      v
  GroupedSchedulingHostedService.StopAsync
      |
      v
  HandlerRegistry.ShutdownAllAsync(30s)
      |
      +--> each StaticSchedulingState.ShutdownAsync
      +--> each InstanceSchedulingState.ShutdownAsync
                |
                +--> cancel owner CTS
                +--> ActionsHandler.ShutdownAsync
```

## What breaks if…

| Change | Failure |
|---|---|
| Strong dictionary for instances | Memory leak of all woven objects ever created |
| No registry | Host cannot find static handlers / undisposed instances |
| Shutdown only statics | Instance work keeps running after “stop” |
| Clear dictionaries without ShutdownAsync | Tokens never canceled; waiters hang |

## See also

- `docs/shutdown.md`
- Hosting chapters (Part V): `AddGroupedScheduling` / hosted service
- Tests: `Engine/ShutdownTests.cs`, `Hosting/HostingTests.cs`
