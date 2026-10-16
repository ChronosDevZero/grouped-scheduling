using System.Collections.Immutable;
using GroupedScheduling;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GroupedScheduling.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsupportedSignatureAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.UnsupportedSignature);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        if (context.Symbol is not IMethodSymbol method)
            return;
        if (!ScheduleGroupFacts.HasScheduleGroup(method))
            return;

        string? reason = null;
        if (!ScheduleGroupFacts.IsSupportedAwaitable(method.ReturnType))
            reason = ScheduleGroupFacts.DescribeUnsupportedReturnType(method.ReturnType);
        else if (HasUnsupportedParameter(method, out var paramReason))
            reason = paramReason;

        if (reason is null)
            return;

        var loc = method.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.UnsupportedSignature,
            loc,
            method.Name,
            reason));
    }

    private static bool HasUnsupportedParameter(IMethodSymbol method, out string reason)
    {
        foreach (var p in method.Parameters)
        {
            if (p.RefKind is RefKind.Ref or RefKind.Out or RefKind.In or RefKind.RefReadOnlyParameter)
            {
                reason = UnsupportedParameterMessages.ByRef(p.Name);
                return true;
            }

            if (p.Type.TypeKind == TypeKind.Pointer)
            {
                reason = UnsupportedParameterMessages.Pointer(p.Name);
                return true;
            }

            if (p.Type is IFunctionPointerTypeSymbol)
            {
                reason = UnsupportedParameterMessages.FunctionPointer(p.Name);
                return true;
            }
        }

        reason = "";
        return false;
    }
}
