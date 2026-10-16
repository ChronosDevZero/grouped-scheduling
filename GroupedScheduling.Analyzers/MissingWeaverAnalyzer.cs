using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GroupedScheduling.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingWeaverAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.MissingWeaver);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var weaverImported = start.Options.AnalyzerConfigOptionsProvider.GlobalOptions
                .TryGetValue(ScheduleGroupFacts.WeaverImportedProperty, out var value)
                && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

            if (weaverImported)
                return;

            start.RegisterSymbolAction(symbolContext =>
            {
                if (symbolContext.Symbol is not IMethodSymbol method)
                    return;
                if (!ScheduleGroupFacts.HasScheduleGroup(method))
                    return;

                var loc = method.Locations.FirstOrDefault(l => l.IsInSource);
                if (loc is null)
                    return;

                symbolContext.ReportDiagnostic(Diagnostic.Create(Diagnostics.MissingWeaver, loc));
            }, SymbolKind.Method);
        });
    }
}
