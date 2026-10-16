# Part VI — Samples as living tutorials

**Project:** `samples/GroupedScheduling.Sample`

Run:

```bash
dotnet run --project samples/GroupedScheduling.Sample
```

## How to study a smoke

1. Read the smoke’s `RunAsync` — what it awaits / asserts.
2. Open the worker method it calls — note `[ScheduleGroup]` name + mode.
3. Mentally replay: stub → hooks → handler → lane/nest.
4. Match the behavior to a chapter in Parts II / II-B / III.

## Host wiring (`Program.cs`)

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddGroupedScheduling();
// register workers...
using var host = builder.Build();
await host.StartAsync();
// run smokes...
await HostStopSmoke.RunAsync(host, ...);  // last — stops the host
```

Two `LifecycleDemoWorker` instances (`lifeA`, `lifeB`) exist specifically for cross-instance nest demos.
