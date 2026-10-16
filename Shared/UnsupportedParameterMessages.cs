namespace GroupedScheduling;

/// <summary>
/// Shared TSCH004 / GSWEAVE005 reason strings for unsupported [ScheduleGroup] parameters.
/// Linked into Analyzers + Weaver (Roslyn vs Cecil still detect separately).
/// </summary>
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
