# Files: `WeaveOptions`, `WeaveResult`, `WeaveDiagnostic`

**Path:** `GroupedScheduling.Weaver/`

## `WeaveOptions`

```csharp
public sealed class WeaveOptions
{
    public List<string> AssemblySearchDirectories { get; } = new();
}
```

Mono.Cecil must **resolve** referenced assemblies (`GroupedScheduling`, BCL, your deps). The MSBuild task fills this from `ReferencePath` item directories. Without it, `Resolve()` on base types / attributes can fail mid-weave.

`Weave(ModuleDefinition, options)` currently reserves options for future knobs (`_ = options` in the module walk) — search directories matter on the **AssemblyIO** entry point.

## `WeaveSeverity` + `WeaveDiagnostic`

```csharp
public enum WeaveSeverity { Info, Warning, Error }

public sealed class WeaveDiagnostic
{
    public WeaveSeverity Severity { get; }
    public string Id { get; }      // e.g. GSWEAVE004
    public string Message { get; }
}
```

IDs are build-visible (`GSWEAVE*` / task logs `GSBUILD*`). Errors fail the weave result so bad IL is not written.

## `WeaveResult`

```csharp
public sealed class WeaveResult
{
    public IReadOnlyList<WeaveDiagnostic> Diagnostics { get; }
    public bool Success => Diagnostics.All(d => d.Severity != WeaveSeverity.Error);
    public int MethodsWoven { get; }
}
```

| Property | Use |
|---|---|
| `Success` | MSBuild task return value |
| `MethodsWoven` | Log line “woven N method(s)” |
| `Diagnostics` | Mapped to `LogError` / `LogWarning` / `LogMessage` |

## ID catalog (weave + build)

See also `docs/diagnostics.md`.

| ID | Meaning |
|---|---|
| GSWEAVE000 | Unexpected exception while weaving a type (includes mode conflicts) |
| GSWEAVE002 | Instance `[ScheduleGroup]` on base and derived |
| GSWEAVE004 | Bad return type |
| GSWEAVE005 | Unsupported parameters |
| GSBUILD001 | Intermediate assembly missing |
| GSBUILD002 | Unhandled task exception |
