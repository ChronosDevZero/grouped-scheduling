using Microsoft.CodeAnalysis;

namespace GroupedScheduling.Analyzers;

internal static class Diagnostics
{
    public const string Category = "GroupedScheduling";

    public static readonly DiagnosticDescriptor NestedSameInstance = new(
        id: "TSCH001",
        title: "Nested scheduled call on the same instance",
        messageFormat: "Nested [ScheduleGroup] call to '{0}' on the same instance is not allowed; use a different instance or extract non-scheduled work",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Same-handler nested entry is forbidden at runtime. Refactor so the outer method does not call another attributed method on this.");

    public static readonly DiagnosticDescriptor MissingWeaver = new(
        id: "TSCH002",
        title: "GroupedScheduling weaver not imported",
        messageFormat: "[ScheduleGroup] is used but the GroupedScheduling weaver MSBuild targets are not imported; add a PackageReference to GroupedScheduling.Build",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Without the weaver package, [ScheduleGroup] has no effect. Reference GroupedScheduling.Build so build targets run after compile.");

    public static readonly DiagnosticDescriptor InheritanceConflict = new(
        id: "TSCH003",
        title: "ScheduleGroup instance methods on base and derived",
        messageFormat: "Type '{0}' has [ScheduleGroup] instance methods but base type '{1}' already does; only one level in the hierarchy may declare them in v1",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "v1 injects a single per-instance handler field; base and derived both declaring instance [ScheduleGroup] methods is unsupported.");

    public static readonly DiagnosticDescriptor UnsupportedSignature = new(
        id: "TSCH004",
        title: "Unsupported ScheduleGroup method signature",
        messageFormat: "[ScheduleGroup] method '{0}' is not supported in v1: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Supported return types are Task, Task<T>, ValueTask, and ValueTask<T>. Generic declaring types and generic methods are allowed; by-ref parameters, void, and sync returns are not.");

    public static readonly DiagnosticDescriptor MissingCancellationToken = new(
        id: "TSCH005",
        title: "ScheduleGroup method has no CancellationToken",
        messageFormat: "[ScheduleGroup] method '{0}' has no CancellationToken; shutdown can cancel queued waits but cannot stop in-flight work that does not observe a token — add a CancellationToken parameter for linked cancel",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Without a CancellationToken parameter the weaver cannot pass a linked shutdown token into the method body.");
}
