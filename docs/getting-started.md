# Getting started

## 1. Reference the package on defining projects

Any project that **declares** `[ScheduleGroup]` methods must reference the weaver package so IL is rewritten after compile:

```xml
<ItemGroup>
  <PackageReference Include="GroupedScheduling.Build" Version="0.1.0" />
</ItemGroup>
```

Downstream apps that only *call* already-woven types need the runtime (brought in by that package) and host registration— they do **not** need to re-weave.

## 2. Register host stop (once per process)

```csharp
using GroupedScheduling.Hosting;

builder.Services.AddGroupedScheduling();
```

On host stop this drains tracked handlers (instances + statics). Prefer this over calling `HandlerRegistry` yourself.

## 3. Attribute methods

```csharp
using GroupedScheduling;

public sealed class OrdersService
{
    [ScheduleGroup("orders", ScheduleMode.ExclusiveSerial)]
    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        // Your code — runs under the group lane after weave.
        await _db.SaveAsync(order, ct);
    }
}
```

Callers stay clean:

```csharp
await orders.SaveAsync(order, ct);
```

Delegates and reflection also hit the woven wrapper (body is scheduled on every entry).

## 4. Disable weaving (optional)

```xml
<PropertyGroup>
  <GroupedSchedulingWeaveEnabled>false</GroupedSchedulingWeaveEnabled>
</PropertyGroup>
```

Without weaving, `[ScheduleGroup]` has no effect (analyzer **TSCH002** fires if attributes are used without imported targets).

## Next

- [Schedule modes](schedule-modes.md)
- [Shutdown & tokens](shutdown.md)
- [Diagnostics](diagnostics.md)
