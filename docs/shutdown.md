# Shutdown & tokens

## Do not rely on GC

Weaving injects `IDisposable` on types with instance `[ScheduleGroup]` methods. Dispose cancels the **object CTS** and shuts the handler down. DI will dispose woven instances when the container does; GC will not.

## Host stop

`AddGroupedScheduling()` registers an `IHostedService` that on stop calls:

```text
HandlerRegistry.ShutdownAllAsync(drainTimeout: 30s)
```

That walks registered instance + static states, cancels tokens, drains in-flight work (best effort), and rejects new schedules.

## CancellationToken on methods

| Method has `CancellationToken`? | Behavior |
|---|---|
| Yes | Weaver passes a **linked** token (object/type CTS + method token). Shutdown and caller cancel can stop in-flight work that observes the token. |
| No | Queued waits can still be cancelled; the body is **not** given a shutdown token. Prefer adding a parameter (**TSCH005** info). |

## After shutdown

New calls to scheduled methods fail with `InvalidOperationException` (“Scheduler is shutting down”).

## Manual shutdown

Advanced / tests may call `HandlerRegistry.ShutdownAllAsync` or dispose an owner. Prefer host registration in apps. Do **not** call process-wide `ShutdownAll` from parallel unit tests that share the registry—it cancels other tests’ handlers.
