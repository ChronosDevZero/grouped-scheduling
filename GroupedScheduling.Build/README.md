# GroupedScheduling

Attribute-driven, **group-aware async scheduling** for .NET with **build-time IL weaving**.

Mark methods with `[ScheduleGroup]`. Callers keep normal `await` / delegates / reflection — the weaver injects scheduling, nesting checks, and linked cancellation. No Fody.

- **NuGet:** [GroupedScheduling.Build](https://www.nuget.org/packages/GroupedScheduling.Build)
- **Source & docs:** [github.com/ChronosDevZero/grouped-scheduling](https://github.com/ChronosDevZero/grouped-scheduling)
- **License:** MIT

## Install

```bash
dotnet add package GroupedScheduling.Build
```

Or in the `.csproj` of any project that **declares** `[ScheduleGroup]` methods:

```xml
<PackageReference Include="GroupedScheduling.Build" Version="0.1.1" />
```

One reference brings:

| Piece | Role |
|---|---|
| `GroupedScheduling` | Runtime API |
| Roslyn analyzers (TSCH001–005) | Catch misuse at design time |
| MSBuild IL weaver | Rewrites attributed methods after compile |

Apps that only *call* already-woven types need the runtime (transitive) and host registration — they do **not** need to re-weave.

## Quick start

**1. Attribute methods**

```csharp
using GroupedScheduling;

public sealed class OrdersService
{
    [ScheduleGroup("orders", ScheduleMode.ExclusiveSerial)]
    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        await _db.SaveAsync(order, ct);
    }
}
```

**2. Call as usual**

```csharp
await orders.SaveAsync(order, ct);
```

**3. Register once in the host process** (graceful stop)

```csharp
using GroupedScheduling.Hosting;

builder.Services.AddGroupedScheduling();
```

On host stop this cancels object/type tokens and rejects new scheduled work. Method authors only need `[ScheduleGroup]`. Callers do not need a per-call `using`. DI still disposes woven `IDisposable` instances when the container disposes them — GC does not call `Dispose`.

## Optional: disable weaving

```xml
<PropertyGroup>
  <GroupedSchedulingWeaveEnabled>false</GroupedSchedulingWeaveEnabled>
</PropertyGroup>
```

Without weaving, `[ScheduleGroup]` has no effect (analyzer **TSCH002** warns if attributes are used without imported targets).

## Learn more

| Topic | Link |
|---|---|
| Getting started | [docs/getting-started.md](https://github.com/ChronosDevZero/grouped-scheduling/blob/main/docs/getting-started.md) |
| Modes / nesting / shutdown | [docs/](https://github.com/ChronosDevZero/grouped-scheduling/tree/main/docs) |
| Diagnostics (TSCH / GSWEAVE) | [docs/diagnostics.md](https://github.com/ChronosDevZero/grouped-scheduling/blob/main/docs/diagnostics.md) |
| Architecture & weaving | [docs/architecture.md](https://github.com/ChronosDevZero/grouped-scheduling/blob/main/docs/architecture.md) |
| Sample app | [samples/GroupedScheduling.Sample](https://github.com/ChronosDevZero/grouped-scheduling/tree/main/samples/GroupedScheduling.Sample) |

**Requires:** .NET 11 (`net11.0`) target for consumers of this package version.
