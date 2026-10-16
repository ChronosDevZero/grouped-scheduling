using System.ComponentModel;

namespace GroupedScheduling;

/// <summary>
/// Per-instance scheduling fields the weaver will inject/store (handler + object CTS).
/// Hidden from IntelliSense; not part of the intended authoring API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InstanceSchedulingState : SchedulingOwnerState
{
    public CancellationToken ObjectToken => OwnerToken;

    public InstanceSchedulingState(Dictionary<int, EventActionGroup> groupMap)
        : base(groupMap)
    {
    }
}
