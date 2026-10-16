# GroupedScheduling

[![NuGet](https://img.shields.io/nuget/v/GroupedScheduling.Build.svg)](https://www.nuget.org/packages/GroupedScheduling.Build)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Attribute-driven, group-aware async scheduling with **build-time IL weaving** (Mono.Cecil MSBuild host). Callers keep normal `await` / delegates / reflection; the weaver injects scheduling, nesting checks, and linked cancellation.

**Install:** [GroupedScheduling.Build on NuGet](https://www.nuget.org/packages/GroupedScheduling.Build)  
**Docs:** [`docs/`](docs/README.md)

## Install

```bash
dotnet add package GroupedScheduling.Build
```

Or in your `.csproj`:

```xml
<PackageReference Include="GroupedScheduling.Build" Version="0.1.1" />
```

## Quick start

1. On projects that **declare** `[ScheduleGroup]` methods, add the package above.

2. In the **host** process:

```csharp
using GroupedScheduling.Hosting;
builder.Services.AddGroupedScheduling();
```

3. Attribute methods:

```csharp
[ScheduleGroup("orders", ScheduleMode.ExclusiveSerial)]
public async Task SaveAsync(CancellationToken ct) { ... }
```

## Build & test

```bash
dotnet build GroupedScheduling.slnx
dotnet test GroupedScheduling.Tests/GroupedScheduling.Tests.csproj
dotnet run --project samples/GroupedScheduling.Sample
dotnet pack GroupedScheduling.Build/GroupedScheduling.Build.csproj -c Release -o artifacts/packages
```

## Docs map

| Topic | Link |
|---|---|
| Getting started | [docs/getting-started.md](docs/getting-started.md) |
| Modes / nesting / shutdown | [docs/](docs/README.md) |
| Diagnostics (TSCH / GSWEAVE) | [docs/diagnostics.md](docs/diagnostics.md) |
| Architecture & weaving | [docs/architecture.md](docs/architecture.md), [docs/weaving.md](docs/weaving.md) |
| Deep dive tutorial (DOCX / Google Docs) | [docs/tutorial/](docs/tutorial/README.md) |
| Sample | [samples/GroupedScheduling.Sample](samples/GroupedScheduling.Sample) |
