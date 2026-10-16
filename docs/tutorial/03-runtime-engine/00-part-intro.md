# Part II-B — Runtime support

These types feed `ActionsHandler`. Read this part after the lane engine chapters.

## Files in order

| Chapter | File | Job |
|---|---|---|
| Nest | `SchedulingNest.cs` | `AsyncLocal` stack; same-handler re-entry throws |
| Runtime helper | `SchedulingRuntime.cs` | Linked call-token factory |
| Owner base | `SchedulingOwnerState.cs` | Handler + owner CTS; shutdown |
| Instance / static | `InstanceSchedulingState` / `StaticSchedulingState` | Per-object vs per-type owners |
| Registry | `HandlerRegistry.cs` | Process-wide discovery + `ShutdownAllAsync` |
| Weave hooks | `SchedulingWeaveHooks.cs` | Stable entry points emitted by the weaver |

## Quick mental links

```text
WeaveHooks.Schedule*
    -> owner state (instance/static)
        -> ActionsHandler.ScheduleTask(..., ownerOrLinkedToken)
            -> Nest + Lane

Host Stop
    -> HandlerRegistry.ShutdownAllAsync
        -> each owner state.ShutdownAsync
```

Hosting registration (`AddGroupedScheduling`) is documented under Part V — read those two short chapters right after the registry if you are wiring a host.
