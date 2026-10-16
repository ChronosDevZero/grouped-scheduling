using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GroupedScheduling.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InheritanceConflictAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.InheritanceConflict);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type || type.TypeKind != TypeKind.Class)
            return;

        if (!ScheduleGroupFacts.InstanceAttributedMethods(type).Any())
            return;

        for (var baseType = type.BaseType;
             baseType is not null && baseType.SpecialType != SpecialType.System_Object;
             baseType = baseType.BaseType)
        {
            if (!ScheduleGroupFacts.InstanceAttributedMethods(baseType).Any())
                continue;

            var loc = type.Locations.FirstOrDefault(l => l.IsInSource);
            if (loc is null)
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.InheritanceConflict,
                loc,
                type.Name,
                baseType.Name));
            return;
        }
    }
}
