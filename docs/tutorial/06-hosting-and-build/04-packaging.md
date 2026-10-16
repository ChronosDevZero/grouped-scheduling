# Packaging: `GroupedScheduling.Build` NuGet

## Why one package

App authors get **runtime + analyzers + MSBuild weaver** from a single `PackageReference`. That prevents “I attributed methods but forgot the weaver” (TSCH002 exists for when props/targets are missing).

## Package layout

| Package path | Content |
|---|---|
| `lib/net11.0/` | `GroupedScheduling.dll` (runtime) |
| `tasks/net11.0/` | MSBuild task, weaver, Cecil, runtime copy for the weave process |
| `analyzers/dotnet/cs/` | `GroupedScheduling.Analyzers.dll` |
| `build/` | `.props` / `.targets` (import weave + `GroupedSchedulingWeaverImported`) |

## Who references what

| Project kind | Reference |
|---|---|
| Declares `[ScheduleGroup]` | `GroupedScheduling.Build` (must weave) |
| Host process only | Runtime + `AddGroupedScheduling()` (often transitive) |
| Consumes pre-woven library | Runtime only; do not re-weave unless you declare new attributes |

## Local repo development

Sample and Fixture import **in-repo** targets instead of a packed package (see their `.csproj`). Same weaver, faster iteration while hacking Cecil.

## Pack command

```bash
dotnet pack GroupedScheduling.Build/GroupedScheduling.Build.csproj -c Release -o artifacts/packages
```

## See also

- Short doc: `docs/packaging.md`
- Props set `GroupedSchedulingWeaverImported` for TSCH002
