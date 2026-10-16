namespace GroupedScheduling;

/// <summary>
/// Concurrency policy for a schedule group.
/// </summary>
public enum ScheduleMode
{
    /// <summary>
    /// Bypass the exclusive group queue; run immediately in parallel with all other work.
    /// </summary>
    FullyConcurrent = 0,

    /// <summary>
    /// Only one group is active at a time; tasks inside the group run one after another.
    /// Waiting groups are activated in FIFO order.
    /// </summary>
    ExclusiveSerial = 1,

    /// <summary>
    /// Only one group is active at a time; tasks inside the group may run in parallel.
    /// Waiting groups are activated in FIFO order.
    /// </summary>
    ExclusiveParallel = 2,
}
