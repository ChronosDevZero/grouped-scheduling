using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GroupedScheduling.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.MissingCancellationToken);

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
        if (method.Parameters.Any(p => p.Type.Name == nameof(CancellationToken)
            && p.Type.ContainingNamespace?.ToDisplayString() == "System.Threading"))
            return;

        var loc = method.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.MissingCancellationToken,
            loc,
            method.Name));
    }
}
