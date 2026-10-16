using System.ComponentModel;

namespace GroupedScheduling;

/// <summary>
/// Per-type static scheduling state (static attributed methods).
/// Hidden from IntelliSense; not part of the intended authoring API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class StaticSchedulingState : SchedulingOwnerState
{
    public CancellationToken TypeToken => OwnerToken;

    public StaticSchedulingState(Dictionary<int, EventActionGroup> groupMap)
        : base(groupMap)
    {
    }
}
