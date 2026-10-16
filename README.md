# GroupedScheduling

Attribute-driven, group-aware async scheduling with **build-time IL weaving** (Mono.Cecil MSBuild host). Callers keep normal `await` / delegates / reflection; the weaver injects scheduling, nesting checks, and linked cancellation.

**Full documentation:** [`docs/`](docs/README.md)

## Quick start

1. On projects that **declare** `[ScheduleGroup]` methods:

```xml
<PackageReference Include="GroupedScheduling.Build" Version="0.1.0" />
```

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
