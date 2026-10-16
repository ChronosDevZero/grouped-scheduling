# File: `GroupedSchedulingHostedService.cs`

**Path:** `GroupedScheduling/Hosting/GroupedSchedulingHostedService.cs`  
**Visibility:** `internal`

## Why this file exists

Implements `IHostedService` so the Generic Host can call shutdown at the right time — after the app has decided to stop, before the process exits.

## Full source

```csharp
using Microsoft.Extensions.Hosting;

namespace GroupedScheduling.Hosting;

internal sealed class GroupedSchedulingHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
        => HandlerRegistry.ShutdownAllAsync(drainTimeout: TimeSpan.FromSeconds(30));
}
```

## Line walkthrough

### `internal sealed`

Not part of the authoring surface. Authors only see `AddGroupedScheduling()`.

### `StartAsync`

```csharp
public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
```

Nothing to start — handlers are created lazily when woven code runs. Returning completed task is the standard no-op.

### `StopAsync`

```csharp
public Task StopAsync(CancellationToken cancellationToken)
    => HandlerRegistry.ShutdownAllAsync(drainTimeout: TimeSpan.FromSeconds(30));
```

| Detail | Why |
|---|---|
| `ShutdownAllAsync` | Statics + instances, cancel + drain |
| `30 seconds` | Best-effort drain bound; avoids hanging host stop forever |
| Ignores `cancellationToken` parameter today | Drain uses its own timeout; host CT is not currently linked |

**Concept:** after 30s, `ActionsHandler.ShutdownAsync` still disposes and cancels waiters even if some tasks ignore cancellation — see the Debug warning in the handler chapter.

## Ordering vs DI dispose

Typical host teardown:

```text
  1) IHostedService.StopAsync  (this type -> registry shutdown)
  2) DI container dispose      (woven IDisposable instances)
```

Owners already shut down by the registry no-op on second dispose (`Interlocked` guard). That overlap is intentional and safe.

## What breaks if…

| Change | Failure |
|---|---|
| No hosted service | Statics may never shut down on host stop |
| Infinite drain timeout | Host stop hangs on stuck tasks |
| Calling ShutdownAll from random middleware | Surprising global cancel |

## See also

- Sample: `HostStopSmoke`
- Tests: `Hosting/HostingTests.cs`
