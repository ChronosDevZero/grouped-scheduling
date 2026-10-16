namespace GroupedScheduling;

/// <summary>
/// Named work group with a <see cref="ScheduleMode"/> concurrency policy.
/// </summary>
public sealed class EventActionGroup
{
    public string Name { get; }
    public ScheduleMode Mode { get; }

    public bool CanRunConcurrently => Mode == ScheduleMode.FullyConcurrent;
    public bool CanRunConcurrentlyInGroup => Mode == ScheduleMode.ExclusiveParallel;

    public EventActionGroup(string name, ScheduleMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Mode = mode;
    }
}
