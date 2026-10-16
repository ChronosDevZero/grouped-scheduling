using Microsoft.CodeAnalysis;

namespace GroupedScheduling.Analyzers;

internal static class ScheduleGroupFacts
{
    public const string AttributeMetadataName = "GroupedScheduling.ScheduleGroupAttribute";
    public const string WeaverImportedProperty = "build_property.GroupedSchedulingWeaverImported";

    public static bool HasScheduleGroup(IMethodSymbol method)
        => method.GetAttributes().Any(IsScheduleGroupAttribute);

    public static bool IsScheduleGroupAttribute(AttributeData attr)
        => attr.AttributeClass?.ToDisplayString() == AttributeMetadataName
           || attr.AttributeClass?.Name is "ScheduleGroupAttribute" or "ScheduleGroup";

    public static bool IsTaskOrTaskOfT(ITypeSymbol type)
        => IsSupportedAwaitable(type) && type is INamedTypeSymbol { Name: "Task" };

    public static bool IsSupportedAwaitable(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named
            || named.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks")
            return false;

        return named.Name switch
        {
            "Task" or "ValueTask" => named.TypeArguments.Length is 0 or 1,
            _ => false,
        };
    }

    public static string DescribeUnsupportedReturnType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
            return "return type must be Task, Task<T>, ValueTask, or ValueTask<T> (void is not supported — use async Task or async ValueTask)";

        if (type.TypeKind is TypeKind.Struct or TypeKind.Class or TypeKind.Interface or TypeKind.Enum
            || type.SpecialType != SpecialType.None)
        {
            return $"return type '{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}' is not supported — use Task, Task<T>, ValueTask, or ValueTask<T> (sync returns are not scheduled)";
        }

        return "return type must be Task, Task<T>, ValueTask, or ValueTask<T>";
    }

    public static IEnumerable<IMethodSymbol> InstanceAttributedMethods(INamedTypeSymbol type)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary && HasScheduleGroup(m));
}
