# Diagnostics

## Roslyn (IDE + build) — `TSCH*`

| ID | Severity | Meaning |
|---|---|---|
| **TSCH001** | Error | Nested `[ScheduleGroup]` call on the same instance (static analysis) |
| **TSCH002** | Error | Attribute used but weaver MSBuild targets not imported (`GroupedScheduling.Build`) |
| **TSCH003** | Error | Instance `[ScheduleGroup]` on both base and derived |
| **TSCH004** | Error | Unsupported signature (return type or parameters) |
| **TSCH005** | Info | No `CancellationToken`; shutdown cannot cancel in-flight body |

Analyzers ship inside `GroupedScheduling.Build` (`analyzers/dotnet/cs/`).

`TSCH002` uses the compiler-visible property `GroupedSchedulingWeaverImported` set by package props when targets are imported.

## Weave (post-compile) — `GSWEAVE*`

Emitted by `SchedulingModuleWeaver` when rewriting fails. Fail the build so bad IL is never shipped.

| ID | Meaning |
|---|---|
| **GSWEAVE000** | Unexpected exception while weaving a type (includes group mode conflicts bubbled from `BuildGroupMap`) |
| **GSWEAVE002** | Inheritance conflict (instance methods on base and derived) |
| **GSWEAVE004** | Return type not Task / ValueTask family |
| **GSWEAVE005** | Unsupported parameters |

(`GSWEAVE001` / `GSWEAVE003` are unused — IDs align with related TSCH numbers where useful.)

## MSBuild task — `GSBUILD*`

| ID | Meaning |
|---|---|
| **GSBUILD001** | Intermediate assembly missing at weave time |
| **GSBUILD002** | Unhandled exception in the weave task |

## Mute / severity (optional)

Users can adjust Roslyn severities via `.editorconfig` (e.g. `dotnet_diagnostic.TSCH005.severity = none`). That does **not** disable weave errors.
