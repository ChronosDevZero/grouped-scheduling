using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace GroupedScheduling.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NestedSameInstanceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.NestedSameInstance);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocation)
            return;

        var containing = context.ContainingSymbol as IMethodSymbol;
        if (containing is null || !ScheduleGroupFacts.HasScheduleGroup(containing))
            return;

        var target = invocation.TargetMethod;
        if (target is null || !ScheduleGroupFacts.HasScheduleGroup(target))
            return;

        // Only same declaring type / same instance handler surface.
        if (!SymbolEqualityComparer.Default.Equals(containing.ContainingType, target.ContainingType))
            return;

        if (containing.IsStatic || target.IsStatic)
            return;

        if (!IsThisReceiver(invocation.Instance))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.NestedSameInstance,
            invocation.Syntax.GetLocation(),
            target.Name));
    }

    private static bool IsThisReceiver(IOperation? instance)
    {
        if (instance is null)
            return true; // implicit this

        return instance switch
        {
            IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } => true,
            IConversionOperation { Operand: var inner } => IsThisReceiver(inner),
            _ => false,
        };
    }
}
