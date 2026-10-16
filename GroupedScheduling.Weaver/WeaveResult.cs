namespace GroupedScheduling.Weaver;

public sealed class WeaveResult
{
    public IReadOnlyList<WeaveDiagnostic> Diagnostics { get; }
    public bool Success => Diagnostics.All(d => d.Severity != WeaveSeverity.Error);
    public int MethodsWoven { get; }

    public WeaveResult(IReadOnlyList<WeaveDiagnostic> diagnostics, int methodsWoven)
    {
        Diagnostics = diagnostics;
        MethodsWoven = methodsWoven;
    }
}
