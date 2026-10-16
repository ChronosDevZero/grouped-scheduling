# GroupedScheduling

Attribute-driven group-aware async scheduling with build-time IL weaving.

Full docs: [`docs/`](../docs/README.md) (getting started, modes, diagnostics, architecture).

## Consumer setup

Add one package reference to the project that declares `[ScheduleGroup]` methods:

```xml
<ItemGroup>
  <PackageReference Include="GroupedScheduling.Build" Version="0.1.0" />
</ItemGroup>
```

That single reference brings the runtime library (`GroupedScheduling`), Roslyn analyzers (TSCH001–005), and the post-compile IL weaver.

Pack:

```bash
dotnet pack GroupedScheduling.Build/GroupedScheduling.Build.csproj -c Release -o artifacts/packages
```

`dotnet build` weaves the assembly after compile. No Fody required.

Disable weaving for a project with:

```xml
<PropertyGroup>
  <GroupedSchedulingWeaveEnabled>false</GroupedSchedulingWeaveEnabled>
</PropertyGroup>
```

## Host apps (graceful stop)

In the process that hosts attributed types, register once:

```csharp
using GroupedScheduling.Hosting;

builder.Services.AddGroupedScheduling();
```

On host stop this cancels object/type tokens and rejects new scheduled work. Method authors only need `[ScheduleGroup]`. Callers do not need a per-call `using`. DI still disposes woven `IDisposable` instances when the container disposes them. GC does not call `Dispose`.
