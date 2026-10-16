# File: `GroupedSchedulingServiceCollectionExtensions.cs`

**Path:** `GroupedScheduling/Hosting/GroupedSchedulingServiceCollectionExtensions.cs`

## Why this file exists

This is the **public hosting API** — the third thing app authors learn after the package reference and `[ScheduleGroup]`:

```csharp
builder.Services.AddGroupedScheduling();
```

One registration → process-level graceful stop via `HandlerRegistry`.

## Full source

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GroupedScheduling.Hosting;

public static class GroupedSchedulingServiceCollectionExtensions
{
    /// <summary>
    /// Registers a hosted service that calls <see cref="HandlerRegistry.ShutdownAll"/> on application stop.
    /// Method authors still only need <c>[ScheduleGroup]</c>; callers need no per-call <c>using</c>.
    /// Object <see cref="IDisposable"/> (injected by the weaver) is still disposed by DI when the container disposes the instance.
    /// </summary>
    public static IServiceCollection AddGroupedScheduling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, GroupedSchedulingHostedService>());
        return services;
    }
}
```

## Line walkthrough

### Namespace

`GroupedScheduling.Hosting` — keeps DI types out of the core attribute namespace so console apps that never host do not feel obligated to reference hosting packages beyond what the package already brings.

### `TryAddEnumerable` + `Singleton<IHostedService, …>`

```csharp
services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IHostedService, GroupedSchedulingHostedService>());
```

| Detail | Why |
|---|---|
| `IHostedService` | Generic Host / ASP.NET calls `StopAsync` on these |
| `Singleton` | One stop hook per process is enough |
| `TryAddEnumerable` | Safe if `AddGroupedScheduling()` is called twice — no duplicate hosted services |

**What breaks if you used `AddSingleton` without TryAddEnumerable?** Double registration → `ShutdownAllAsync` might run twice (usually idempotent per owner, but wasteful and surprising).

### Return `services`

Fluent DI style; allows chaining.

## What this does *not* do

- Does not weave assemblies.
- Does not register your worker types.
- Does not replace per-instance `IDisposable` from the weaver — DI still disposes woven instances when the container disposes them.

Host stop drains **registry-tracked** owners (including statics). Instance dispose cancels that object’s owner CTS earlier in many apps when the scope ends.

## See also

- Next: `GroupedSchedulingHostedService`
- `docs/getting-started.md`, `docs/shutdown.md`
