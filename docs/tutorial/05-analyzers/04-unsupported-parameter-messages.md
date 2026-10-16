# File: `Shared/UnsupportedParameterMessages.cs`

**Path:** `Shared/UnsupportedParameterMessages.cs`  
**Visibility:** `internal` — linked into **both** Analyzers and Weaver projects.

## Why this file exists

Roslyn and Cecil each detect unsupported parameter shapes separately (different type systems). Users should see the **same reason strings** whether the IDE or the weaver complains.

```csharp
internal static class UnsupportedParameterMessages
{
    public static string ByRef(string name)
        => $"by-ref parameter '{name}' is not supported";

    public static string Pointer(string name)
        => $"pointer parameter '{name}' is not supported";

    public static string FunctionPointer(string name)
        => $"function-pointer parameter '{name}' is not supported";

    public static string UnsupportedTypeShape(string name)
        => $"parameter '{name}' has an unsupported type shape";
}
```

## How it is shared

Each consuming `.csproj` includes the file as a linked compile item (same source, two assemblies). There is no shared DLL — analyzers stay `netstandard2.0` without dragging the runtime package graph oddly.

## Why these shapes are banned

Closures / `Func` captures and Cecil remaps assume by-value-ish parameters. By-ref and pointers do not round-trip cleanly into a deferred `Func` invoked after the lane grants.

## Weaver-only extra

Cecil also rejects `RequiredModifierType` / `OptionalModifierType` via `UnsupportedTypeShape`. The Roslyn analyzer focuses on RefKind / pointer / function pointer — the common C# cases.

## See also

- TSCH004 / GSWEAVE005
- `docs/signatures.md`
