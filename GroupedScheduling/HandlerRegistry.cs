using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GroupedScheduling;

/// <summary>
/// Tracks live instance and static scheduling states for process-wide shutdown.
/// Prefer <c>AddGroupedScheduling()</c> for host stop; this type is hidden from IntelliSense.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class HandlerRegistry
{
    private static readonly ConditionalWeakTable<object, InstanceSchedulingState> s_instances = new();
    private static readonly ConcurrentDictionary<Type, StaticSchedulingState> s_statics = new();

    public static InstanceSchedulingState GetOrCreateInstanceState(
        object owner,
        Func<InstanceSchedulingState> factory)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(factory);
        return s_instances.GetValue(owner, _ => factory());
    }

    public static bool TryGetInstanceState(object owner, out InstanceSchedulingState? state)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (s_instances.TryGetValue(owner, out var found))
        {
            state = found;
            return true;
        }

        state = null;
        return false;
    }

    public static StaticSchedulingState GetOrCreateStaticState(Type ownerType, Func<StaticSchedulingState> factory)
    {
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(factory);
        return s_statics.GetOrAdd(ownerType, _ => factory());
    }

    public static void ShutdownAll(TimeSpan? drainTimeout = null)
        => ShutdownAllAsync(drainTimeout).GetAwaiter().GetResult();

    public static async Task ShutdownAllAsync(TimeSpan? drainTimeout = null)
    {
        var tasks = new List<Task>();
        foreach (var pair in s_statics)
            tasks.Add(pair.Value.ShutdownAsync(drainTimeout));

        foreach (var pair in s_instances)
            tasks.Add(pair.Value.ShutdownAsync(drainTimeout));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
