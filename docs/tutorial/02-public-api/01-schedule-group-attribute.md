# File: `ScheduleGroupAttribute.cs`

**Path:** `GroupedScheduling/ScheduleGroupAttribute.cs`

## Why this file exists

This is the **only type most app authors must learn**. It marks a method for group-aware scheduling. The weaver discovers methods that carry this attribute and rewrites them. Without the attribute, the weaver does nothing to that method.

## Full source

```csharp
namespace GroupedScheduling;

/// <summary>
/// Marks a method for group-aware scheduling. The weaver wires this to an <see cref="ActionsHandler"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ScheduleGroupAttribute : Attribute
{
    public string Name { get; }
    public ScheduleMode Mode { get; }

    public ScheduleGroupAttribute(string name, ScheduleMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Mode = mode;
    }
}
```

## Concepts you need first

**Attributes in .NET** are metadata. Applying `[ScheduleGroup(...)]` does not, by itself, change method behavior. Something must **read** the metadata:

- Roslyn analyzers → IDE / build diagnostics.
- Mono.Cecil weaver → IL rewrite after compile.

If weaving is disabled, the attribute is decorative (and analyzer TSCH002 may warn).

## Line walkthrough

### Namespace and summary

`namespace GroupedScheduling` — public package root. App code typically `using GroupedScheduling;`.

The XML summary tells authors the weaver wires the attribute to an `ActionsHandler`. That sentence is the contract: attribute → handler → lane.

### `[AttributeUsage(...)]`

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
```

| Argument | Meaning here |
|---|---|
| `AttributeTargets.Method` | Only methods — not classes or properties. Scheduling is per-entry-point. |
| `AllowMultiple = false` | One group identity per method. Two attributes would be ambiguous. |
| `Inherited = false` | A base method’s attribute does **not** automatically apply to an `override`. Inheritance conflicts are a separate analyzer concern. |

**What breaks if `Inherited = true`:** overrides could silently join a group you did not declare on the derived method, making weave and diagnostics harder to reason about.

### Class shape

```csharp
public sealed class ScheduleGroupAttribute : Attribute
```

- `public` — authors must reference it.
- `sealed` — no subclassing; keeps metadata scanning simple.
- Derives from `Attribute` — required for C# attribute syntax.

### Properties

```csharp
public string Name { get; }
public ScheduleMode Mode { get; }
```

Immutable after construction. The weaver and runtime treat `(Name, Mode)` as the group’s identity and policy. Same `Name` on one type must share the same `Mode` (enforced at weave time).

### Constructor

```csharp
public ScheduleGroupAttribute(string name, ScheduleMode mode)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    Name = name;
    Mode = mode;
}
```

**`ThrowIfNullOrWhiteSpace(name)`** — empty or whitespace names are rejected early. A blank name would produce useless diagnostics and colliding “anonymous” groups.

`Mode` is an enum; invalid casts are a compile-time concern for C# callers. The weaver still validates consistency across methods that share a name.

## Authoring example

```csharp
[ScheduleGroup("orders", ScheduleMode.ExclusiveSerial)]
public async Task SaveAsync(Order order, CancellationToken ct)
{
    await _db.SaveAsync(order, ct);
}
```

Callers still write `await orders.SaveAsync(order, ct);`. The attribute does not change the call site — the **woven body** does.

## What breaks if…

| Change | Failure |
|---|---|
| Remove the attribute but keep weave package | Method runs unscheduled; nest/lane rules skipped |
| Allow multiple attributes | Ambiguous group; weaver would need new merge rules |
| Allow whitespace names | Silent bad group keys / confusing errors |

## See also

- Short doc: `docs/getting-started.md`
- Modes: next chapter + `docs/schedule-modes.md`
- Analyzer package: `GroupedScheduling.Analyzers` (later part)
