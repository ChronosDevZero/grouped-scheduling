# File: `SchedulingModuleWeaver.cs` (discovery + state injection)

**Path:** `GroupedScheduling.Weaver/SchedulingModuleWeaver.cs`  
**Shape:** `public static partial class` — split across Rewrite / Dispose / AssemblyIO files.

## Why this file exists

Orchestrates one module:

1. Walk all types (including nested).
2. Find attributed methods; skip already-woven.
3. Validate returns, parameters, inheritance.
4. Build group maps; inject fields / factories / ctor prologue / cctor / dispose.
5. Call `RewriteMethod` per attributed method.

## Entry: `Weave`

```csharp
public static WeaveResult Weave(ModuleDefinition module, WeaveOptions? options = null)
{
    _ = options;
    var diagnostics = new List<WeaveDiagnostic>();
    var woven = 0;
    var scheduleAttrRef = module.ImportReference(typeof(ScheduleGroupAttribute));

    foreach (var type in module.Types.ToArray())
        woven += WeaveTypeTree(module, type, scheduleAttrRef, diagnostics);

    return new WeaveResult(diagnostics, woven);
}
```

| Detail | Why |
|---|---|
| `ImportReference(typeof(ScheduleGroupAttribute))` | Compare attribute types by full name against metadata |
| `Types.ToArray()` | Safe if nested walk mutates collections |
| Sum `woven` | Count of successfully rewritten methods |

## `WeaveTypeTree`

Skips interfaces, enums, and compiler-generated names containing `<` (display classes, async state machines — those are not author types you attribute). Recurses into `NestedTypes`.

Per-type failures become `GSWEAVE000` and do not crash the whole module walk:

```csharp
catch (Exception ex)
{
    diagnostics.Add(Error("GSWEAVE000", $"Failed weaving '{type.FullName}': {ex.Message}"));
}
```

Mode conflicts from `BuildGroupMap` throw `InvalidOperationException` and surface here as GSWEAVE000.

## `WeaveType` — discovery filter

```csharp
var attributed = type.Methods
    .Where(m => !m.IsConstructor && m.HasBody && m.HasCustomAttributes)
    .Select(m => (Method: m, Attr: FindScheduleAttribute(m, scheduleAttrRef)))
    .Where(x => x.Attr is not null)
    .Where(x => type.Methods.All(m => m.Name != "__" + x.Method.Name + "_Original"))
    ...
```

| Filter | Meaning |
|---|---|
| Not constructor | Ctors are not schedule entry points |
| HasBody | Abstract / extern skipped |
| Has attribute | Must be `[ScheduleGroup]` |
| No `__Name_Original` | **Idempotency** — safe re-weave |

## Inheritance guard (GSWEAVE002)

```csharp
if (instanceMethods.Count > 0 && HasAttributedInstanceAncestor(type, scheduleAttrRef))
{
    diagnostics.Add(Error("GSWEAVE002", ...));
    return 0;
}
```

v1 injects a **single** instance state field name. Base + derived both declaring instance scheduled methods is unsupported (mirrors TSCH003). Detection also looks for an existing `__groupedSchedulingState` field on ancestors.

## Validation loop (GSWEAVE004 / 005)

For each attributed method:

- Return must be `Task` / `Task<T>` / `ValueTask` / `ValueTask<T>`.
- Parameters must not be by-ref, pointers, function pointers, or modreq/modopt shapes (messages from `UnsupportedParameterMessages`).

Any failure → diagnostic + `return 0` for the type (no partial weave of that type’s methods).

## Group map

```csharp
private sealed record GroupInfo(int Id, string Name, ScheduleMode Mode);

// ids start at 1, assigned in first-seen order
map[attr.Name] = new GroupInfo(next++, attr.Name, attr.Mode);
// conflict: same name, different mode -> throw
```

Instance methods and static methods get **separate** maps / handlers / fields.

## State injection (instance)

When any instance attributed methods exist:

1. `EnsureInstanceStateField` — private `__groupedSchedulingState`.
2. `EnsureCreateStateMethod` — `__GS_CreateInstanceState` builds `Dictionary<int, EventActionGroup>` then `new InstanceSchedulingState(dict)`.
3. `WeaveInstanceConstructors` — after base ctor call, `field = InitInstanceState(this, createFactory)`.
4. `EnsureDispose` — see Dispose chapter.

### Create-state IL (concept)

```text
  dict = new Dictionary<int, EventActionGroup>()
  dict.Add(1, new EventActionGroup("orders", ExclusiveSerial))
  dict.Add(2, new EventActionGroup("reports", FullyConcurrent))
  return new InstanceSchedulingState(dict)
```

### Constructor prologue

```text
  // after base/.ctor call
  ldarg.0
  ldarg.0
  ldnull
  ldftn __GS_CreateInstanceState
  newobj Func<InstanceSchedulingState>(object, IntPtr)
  call SchedulingWeaveHooks.InitInstanceState
  stfld __groupedSchedulingState
```

Skips ctors that chain to `this(...)` so initialization runs once from the ctor that calls `base`.

## State injection (static)

1. `EnsureStaticStateField` — private static `__groupedSchedulingStaticState`.
2. Same create method pattern for `StaticSchedulingState`.
3. `EnsureStaticInitializer` — `.cctor` prologue:

```text
  field = HandlerRegistry.GetOrCreateStaticState(typeof(ClosedT), createFactory)
```

Uses `SelfClosedReference` so open generic definitions emit `typeof(Foo<T>)` correctly for each closed runtime type.

## Rewrite loop

```csharp
foreach (var (method, attr) in instanceMethods)
{
    RewriteMethod(..., instanceField, instanceGroups[attr.Name].Id, isStatic: false);
    woven++;
}
// same for static
```

Group **id** (int) is what the stub loads — not the string name — for a cheap hook argument.

## `FindScheduleAttribute`

Reads constructor args: `[0]` name string, `[1]` mode enum (as int). Whitespace name → treat as missing.

## `SelfClosedReference`

For `Foo<T>`, returns `Foo<!0>` so `ldtoken` / `ldfld` bind to the closed instantiation at runtime. Critical for generics.

## What breaks if…

| Change | Failure |
|---|---|
| Skip idempotency check | Double-wrap → broken nesting of stubs |
| One group map for instance+static | Shared handler incorrectly |
| Init in every `this()` ctor | Double `InitInstanceState` / confused field |
| Open `typeof(Foo<>)` in cctor | Wrong static registry key |

## See also

- Short doc: `docs/weaving.md`
- Next: Rewrite
