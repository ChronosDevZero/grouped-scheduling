# Analyzers TSCH001–005 (deep dive)

Each analyzer follows the same shell:

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class X : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ...;
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // RegisterSymbolAction / RegisterOperationAction / RegisterCompilationStartAction
    }
}
```

| Setting | Why |
|---|---|
| Ignore generated code | Do not squiggle compiler-generated noise |
| Concurrent execution | Scale in large solutions |

---

## TSCH001 — `NestedSameInstanceAnalyzer`

**Kind:** `OperationKind.Invocation` inside a scheduled method.

**Fires when all are true:**

1. Containing method has `[ScheduleGroup]`.
2. Target method has `[ScheduleGroup]`.
3. Same containing type.
4. Neither is static.
5. Receiver is `this` (explicit or implicit).

**Does not catch:** reflection, different instances, static↔instance (different handlers). Runtime nest still authoritative — see sample `NestRejectSmoke`.

**Fix:** extract non-scheduled helper, or call through another instance.

---

## TSCH002 — `MissingWeaverAnalyzer`

**Kind:** compilation start → if weaver property not `true`, register method symbol actions.

```csharp
var weaverImported = GlobalOptions.TryGetValue("build_property.GroupedSchedulingWeaverImported", ...)
    && value == "true";
if (weaverImported) return; // nothing to do
// else report on every attributed method
```

**Why compilation-start:** property is per-compilation; avoid re-reading per method incorrectly.

**Fix:** `PackageReference` to `GroupedScheduling.Build` (or import in-repo targets like Sample/Fixture).

---

## TSCH003 — `InheritanceConflictAnalyzer`

**Kind:** named type symbol (classes).

If this type has instance attributed methods, walk base types; if any base also has them → error with both type names.

**Why:** v1 single instance field name / handler injection story. Mirrors GSWEAVE002.

---

## TSCH004 — `UnsupportedSignatureAnalyzer`

**Kind:** method symbol with attribute.

Checks:

1. Supported awaitable return (else describe why).
2. No by-ref / pointer / function-pointer params (shared `UnsupportedParameterMessages`).

Reports at method source location with `(methodName, reason)`.

---

## TSCH005 — `MissingCancellationTokenAnalyzer`

**Kind:** method symbol with attribute.

If no parameter typed `System.Threading.CancellationToken` → **Info** diagnostic.

Not an error: weaving still works; shutdown simply cannot pass a linked token into the body.

**Fix:** add `CancellationToken ct` and use it in awaits.

---

## Analyzer vs weaver cheat sheet

```text
  IDE red squiggle (TSCH*)     -->  fix source early
  Build weave error (GSWEAVE*) -->  last line of defense
  Runtime InvalidOperation     -->  nest via reflection / dynamic
```
