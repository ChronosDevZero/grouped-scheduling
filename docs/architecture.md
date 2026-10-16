# Architecture

## Solution map

| Project | Role |
|---|---|
| `GroupedScheduling` | Runtime: attribute, modes, `ActionsHandler`, nest, registry, weave hooks, hosting |
| `GroupedScheduling.Weaver` | Host-agnostic Mono.Cecil transforms |
| `GroupedScheduling.Build` | NuGet package: MSBuild task + packs runtime, weaver, analyzers |
| `GroupedScheduling.Analyzers` | Roslyn diagnostics TSCH001–005 |
| `GroupedScheduling.Fixture` | Woven workers / weave subjects for tests |
| `GroupedScheduling.Tests` | Engine, weave, analyzer, worker, hosting tests |
| `samples/GroupedScheduling.Sample` | Console host + smoke modules |

## End-to-end flow

```text
  Source with [ScheduleGroup]
           │
           ▼
  Roslyn analyzers (TSCH*) ── red squiggles / build errors
           │
           ▼
  CSC → intermediate DLL
           │
           ▼
  MSBuild GroupedSchedulingWeave (AfterTargets=CoreCompile)
           │  Mono.Cecil rewrite via WeaveAssembly task
           ▼
  Woven DLL (wrappers + fields + dispose)
           │
           ▼
  Runtime: SchedulingWeaveHooks → ActionsHandler → GroupLaneScheduler
           │
           ▼
  Host StopAsync → HandlerRegistry.ShutdownAllAsync
```

## Runtime pieces

| Type | Job |
|---|---|
| `ActionsHandler` | Schedule into groups; track in-flight; shutdown |
| `GroupLaneScheduler` | Exclusive lane: one active group, FIFO waiters, serial vs parallel grants |
| `EventActionGroup` | Name + `ScheduleMode` |
| `SchedulingNest` | `AsyncLocal` stack; same-handler re-entry throws |
| `InstanceSchedulingState` / `StaticSchedulingState` | Handler + owner CTS per object / type |
| `HandlerRegistry` | Weak table of instances + concurrent static map for process shutdown |
| `SchedulingWeaveHooks` | Stable entry points emitted by the weaver (`[EditorBrowsable(Never)]`) |
| `AddGroupedScheduling()` | Public hosting API |

## Intended authoring surface

| Surface | Types |
|---|---|
| Happy path | `ScheduleGroupAttribute`, `ScheduleMode`, `AddGroupedScheduling()` |
| Advanced / tests | `ActionsHandler`, `EventActionGroup` (manual schedule without attributes) |

Weave plumbing (`SchedulingWeaveHooks`, nest, registry, owner states) is public for Cecil/`typeof` binding but marked `[EditorBrowsable(Never)]`. `GroupLaneScheduler` and `GroupedSchedulingHostedService` are `internal`.

## Why weaving (not interceptors / Harmony)

Attributed methods must stay scheduled when taken as a **method group** or invoked via **delegate** / **reflection**. Body wrapping guarantees every entry path.
