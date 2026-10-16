using System.ComponentModel;

namespace GroupedScheduling;

/// <summary>
/// Tracks which <see cref="ActionsHandler"/> instances are active on the current async flow.
/// Same-handler re-entry throws; different handlers (A→B) are allowed.
/// Hidden from IntelliSense; not part of the intended authoring API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SchedulingNest
{
    private static readonly AsyncLocal<NestStack?> s_stack = new();

    public static bool IsActive(ActionsHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return s_stack.Value?.Contains(handler) == true;
    }

    public static void ThrowIfActive(ActionsHandler handler)
    {
        if (IsActive(handler))
        {
            throw new InvalidOperationException(
                "Nested scheduled call on the same ActionsHandler is not allowed. " +
                "Cross-instance calls (A→B) are fine; same-instance re-entry is not.");
        }
    }

    /// <summary>
    /// Pushes <paramref name="handler"/> onto the nest stack for the duration of the scope.
    /// Call <see cref="ThrowIfActive"/> before scheduling when detecting nested entry.
    /// </summary>
    public static NestScope Enter(ActionsHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfActive(handler);

        var next = NestStack.Push(s_stack.Value, handler);
        s_stack.Value = next;
        return new NestScope(next);
    }

    public readonly struct NestScope : IDisposable
    {
        private readonly NestStack _frame;

        internal NestScope(NestStack frame) => _frame = frame;

        public void Dispose()
        {
            if (!ReferenceEquals(s_stack.Value, _frame))
                return;

            s_stack.Value = _frame.Previous;
        }
    }

    internal sealed class NestStack
    {
        public ActionsHandler Handler { get; }
        public NestStack? Previous { get; }

        private NestStack(ActionsHandler handler, NestStack? previous)
        {
            Handler = handler;
            Previous = previous;
        }

        public static NestStack Push(NestStack? previous, ActionsHandler handler)
            => new(handler, previous);

        public bool Contains(ActionsHandler handler)
        {
            for (var node = this; node is not null; node = node.Previous)
            {
                if (ReferenceEquals(node.Handler, handler))
                    return true;
            }

            return false;
        }
    }
}
