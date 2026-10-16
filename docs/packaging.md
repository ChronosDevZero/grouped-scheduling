# Packaging

## NuGet: `GroupedScheduling.Build`

| Package path | Content |
|---|---|
| `lib/net11.0/` | `GroupedScheduling.dll` (runtime) |
| `tasks/net11.0/` | MSBuild task, weaver, Cecil, runtime copy for weave process |
| `analyzers/dotnet/cs/` | `GroupedScheduling.Analyzers.dll` |
| `build/` | `.props` / `.targets` |

Pack:

```bash
dotnet pack GroupedScheduling.Build/GroupedScheduling.Build.csproj -c Release -o artifacts/packages
```

## Who references what

| Project kind | Reference |
|---|---|
| Declares `[ScheduleGroup]` | `GroupedScheduling.Build` (weaves on build) |
| Host process only | Runtime + `AddGroupedScheduling()` (often already transitive) |
| Library consumed as pre-woven DLL | Consumers need runtime; weave already done |

## Local / repo development

Sample and Fixture import in-repo targets instead of a packed package (see their `.csproj`). Same weaver, easier iteration.

## Build & test

```bash
dotnet build GroupedScheduling.slnx
dotnet test GroupedScheduling.Tests/GroupedScheduling.Tests.csproj
dotnet run --project samples/GroupedScheduling.Sample
```
