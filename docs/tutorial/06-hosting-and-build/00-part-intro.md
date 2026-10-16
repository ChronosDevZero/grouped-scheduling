# Part V — Hosting and build

## Chapters

| Topic | Status |
|---|---|
| `AddGroupedScheduling()` | Done |
| `GroupedSchedulingHostedService` | Done |
| `WeaveAssembly` MSBuild task + targets | Done |
| NuGet packaging layout | Done |

## One-liner for apps

```csharp
builder.Services.AddGroupedScheduling();
```

Registers a singleton `IHostedService` that drains all tracked handlers on host stop (30s drain).

Declaring projects still need `GroupedScheduling.Build` so the weave target runs after compile.
