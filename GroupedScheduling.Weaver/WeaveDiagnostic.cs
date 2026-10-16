namespace GroupedScheduling.Weaver;

public enum WeaveSeverity
{
    Info,
    Warning,
    Error,
}

public sealed class WeaveDiagnostic
{
    public WeaveSeverity Severity { get; }
    public string Id { get; }
    public string Message { get; }

    public WeaveDiagnostic(WeaveSeverity severity, string id, string message)
    {
        Severity = severity;
        Id = id;
        Message = message;
    }

    public override string ToString() => $"{Severity} {Id}: {Message}";
}
