# File: `Diagnostics.cs`

**Path:** `GroupedScheduling.Analyzers/Diagnostics.cs`

## Why this file exists

Central catalog of `DiagnosticDescriptor` instances. Each analyzer reports one (or more) of these. Keeping descriptors here avoids drift in id/title/message across files.

## Descriptors (teaching view)

| ID | Severity | One-line meaning |
|---|---|---|
| TSCH001 | Error | Nested scheduled call on same instance (static analysis) |
| TSCH002 | Error | Attribute used but weaver targets not imported |
| TSCH003 | Error | Instance ScheduleGroup on base and derived |
| TSCH004 | Error | Unsupported signature |
| TSCH005 | Info | No CancellationToken — shutdown cannot cancel in-flight body |

All use category `"GroupedScheduling"`.

## Message design

Messages tell authors **what to do**, not only what is wrong:

- TSCH001 → use different instance or extract non-scheduled work.
- TSCH002 → add `PackageReference` to `GroupedScheduling.Build`.
- TSCH005 → add a `CancellationToken` for linked cancel.

## EditorConfig

Users may soften TSCH005 (`severity = none`). That does **not** disable weave errors.

## See also

- `docs/diagnostics.md`
