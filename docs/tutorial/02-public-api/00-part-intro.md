# Part II — Public API and the lane engine

This part is the heart of the runtime. If you only deeply learn one slice of the repo, learn this.

## Files in order

1. `ScheduleGroupAttribute.cs` — authoring surface.
2. `ScheduleMode.cs` — concurrency policy enum.
3. `EventActionGroup.cs` — runtime group identity + mode helpers.
4. `ActionsHandler.cs` — schedule, track, shut down.
5. `GroupLaneScheduler.cs` — exclusive FIFO lane (internal).

Later phases add nest, owner state, registry, weave hooks, and hosting — they *feed* this engine; they do not replace it.

## Intended vs hidden API

| Surface | Types |
|---|---|
| Happy path for app authors | `ScheduleGroupAttribute`, `ScheduleMode`, `AddGroupedScheduling()` |
| Advanced / tests (manual schedule) | `ActionsHandler`, `EventActionGroup` |
| Weaver plumbing (public but hidden from IntelliSense) | `SchedulingWeaveHooks`, nest, registry, owner states |

`GroupLaneScheduler` and `GroupedSchedulingHostedService` are `internal`.
