namespace GroupedScheduling;

/// <summary>
/// Marks a method for group-aware scheduling. The weaver wires this to an <see cref="ActionsHandler"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ScheduleGroupAttribute : Attribute
{
    public string Name { get; }
    public ScheduleMode Mode { get; }

    public ScheduleGroupAttribute(string name, ScheduleMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Mode = mode;
    }
}
