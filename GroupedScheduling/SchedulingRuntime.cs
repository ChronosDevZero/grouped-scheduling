using System.ComponentModel;

namespace GroupedScheduling;

/// <summary>
/// Thin helpers intended for weaver-generated IL and shared runtime entry points.
/// Nest detection lives inside <see cref="ActionsHandler"/> around the running work.
/// Hidden from IntelliSense; not part of the intended authoring API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SchedulingRuntime
{
    /// <summary>
    /// Links an object-level shutdown token with an optional per-call token.
    /// Dispose the returned <see cref="CancellationTokenSource"/> when the call completes.
    /// </summary>
    public static CancellationTokenSource CreateLinkedCallToken(
        CancellationToken objectToken,
        CancellationToken methodToken = default)
    {
        if (methodToken.CanBeCanceled)
            return CancellationTokenSource.CreateLinkedTokenSource(objectToken, methodToken);

        if (objectToken.CanBeCanceled)
            return CancellationTokenSource.CreateLinkedTokenSource(objectToken);

        return new CancellationTokenSource();
    }
}
