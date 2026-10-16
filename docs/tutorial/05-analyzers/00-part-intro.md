# Part IV — Analyzers

Roslyn analyzers give **early** feedback in the IDE. The weaver remains authoritative for post-compile truth (especially reflection nests).

## Project

`GroupedScheduling.Analyzers` — TFM `netstandard2.0`, shipped inside `GroupedScheduling.Build`.

## Chapters

1. `Diagnostics.cs` — ID catalog.
2. `ScheduleGroupFacts.cs` — shared classification.
3. Each analyzer (TSCH001–005).
4. `Shared/UnsupportedParameterMessages.cs` — shared with weaver.

## Dual detection

| Concern | Roslyn | Weave |
|---|---|---|
| Bad signature | TSCH004 | GSWEAVE004/005 |
| Inheritance | TSCH003 | GSWEAVE002 |
| Nest (obvious) | TSCH001 | Runtime throw |
| Missing weaver | TSCH002 | (attribute inert) |
| Missing CT | TSCH005 (info) | (behavior difference only) |
