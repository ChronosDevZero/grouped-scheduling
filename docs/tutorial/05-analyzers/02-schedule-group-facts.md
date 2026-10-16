# File: `ScheduleGroupFacts.cs`

**Path:** `GroupedScheduling.Analyzers/ScheduleGroupFacts.cs`

## Why this file exists

Shared helpers so every analyzer agrees on:

- What counts as `[ScheduleGroup]`
- What return types are supported awaitables
- How to find instance attributed methods on a type
- The MSBuild property name that means “weaver targets imported”

## Constants

```csharp
public const string AttributeMetadataName = "GroupedScheduling.ScheduleGroupAttribute";
public const string WeaverImportedProperty = "build_property.GroupedSchedulingWeaverImported";
```

`build_property.*` is how analyzers read MSBuild properties via `.globalconfig` / analyzer config options. Package props set `GroupedSchedulingWeaverImported=true` when targets import.

## `HasScheduleGroup` / `IsScheduleGroupAttribute`

Accepts full metadata name **or** short names `ScheduleGroupAttribute` / `ScheduleGroup` (common when looking at attribute class name alone).

## `IsSupportedAwaitable`

Must live in `System.Threading.Tasks` and be named `Task` or `ValueTask` with 0 or 1 type arguments. Mirrors weaver `IsSupportedAwaitable`.

## `DescribeUnsupportedReturnType`

Friendly reasons for void vs sync returns — used in TSCH004 messages.

## `InstanceAttributedMethods`

Ordinary instance methods with the attribute — used by inheritance conflict analyzer.
