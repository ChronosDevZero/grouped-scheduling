using System.ComponentModel;

namespace GroupedScheduling;

/// <summary>
/// Entry points emitted by the IL weaver. Keep signatures stable — the weaver binds by metadata name.
/// Hidden from IntelliSense; not part of the intended authoring API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SchedulingWeaveHooks
{
    public const string InstanceStateFieldName = "__groupedSchedulingState";
    public const string StaticStateFieldName = "__groupedSchedulingStaticState";
    public const string CreateInstanceStateMethodName = "__GS_CreateInstanceState";
    public const string CreateStaticStateMethodName = "__GS_CreateStaticState";

    public static InstanceSchedulingState InitInstanceState(object owner, Func<InstanceSchedulingState> factory)
        => HandlerRegistry.GetOrCreateInstanceState(owner, factory);

    public static Task Schedule(
        InstanceSchedulingState state,
        int groupId,
        Func<Task> work)
        => state.Handler.ScheduleTask(work, groupId, state.ObjectToken);

    public static Task<T> Schedule<T>(
        InstanceSchedulingState state,
        int groupId,
        Func<Task<T>> work)
        => state.Handler.ScheduleTask(work, groupId, state.ObjectToken);

    public static Task Schedule(
        InstanceSchedulingState state,
        int groupId,
        Func<CancellationToken, Task> work,
        CancellationToken methodToken)
    {
        var linked = state.CreateCallToken(methodToken);
        return RunWithLinkedToken(state.Handler, groupId, linked, work);
    }

    public static Task<T> Schedule<T>(
        InstanceSchedulingState state,
        int groupId,
        Func<CancellationToken, Task<T>> work,
        CancellationToken methodToken)
    {
        var linked = state.CreateCallToken(methodToken);
        return RunWithLinkedToken(state.Handler, groupId, linked, work);
    }

    public static Task ScheduleStatic(
        StaticSchedulingState state,
        int groupId,
        Func<Task> work)
        => state.Handler.ScheduleTask(work, groupId, state.TypeToken);

    public static Task<T> ScheduleStatic<T>(
        StaticSchedulingState state,
        int groupId,
        Func<Task<T>> work)
        => state.Handler.ScheduleTask(work, groupId, state.TypeToken);

    public static Task ScheduleStatic(
        StaticSchedulingState state,
        int groupId,
        Func<CancellationToken, Task> work,
        CancellationToken methodToken)
    {
        var linked = state.CreateCallToken(methodToken);
        return RunWithLinkedToken(state.Handler, groupId, linked, work);
    }

    public static Task ScheduleStatic<T>(
        StaticSchedulingState state,
        int groupId,
        Func<CancellationToken, Task<T>> work,
        CancellationToken methodToken)
    {
        var linked = state.CreateCallToken(methodToken);
        return RunWithLinkedToken(state.Handler, groupId, linked, work);
    }

    public static ValueTask Schedule(
        InstanceSchedulingState state,
        int groupId,
        Func<ValueTask> work)
        => new(state.Handler.ScheduleTask(() => work().AsTask(), groupId, state.ObjectToken));

    public static ValueTask<T> Schedule<T>(
        InstanceSchedulingState state,
        int groupId,
        Func<ValueTask<T>> work)
        => new(state.Handler.ScheduleTask(() => work().AsTask(), groupId, state.ObjectToken));

    public static ValueTask Schedule(
        InstanceSchedulingState state,
        int groupId,
        Func<CancellationToken, ValueTask> work,
        CancellationToken methodToken)
    {
        var linked = state.CreateCallToken(methodToken);
        return new ValueTask(RunWithLinkedToken(state.Handler, groupId, linked, work));
    }

    public static ValueTask<T> Schedule<T>(
        InstanceSchedulingState state,
        int groupId,
        Func<CancellationToken, ValueTask<T>> work,
        CancellationToken methodToken)
    {
        var linked = state.CreateCallToken(methodToken);
        return new ValueTask<T>(RunWithLinkedToken(state.Handler, groupId, linked, work));
    }

    public static ValueTask ScheduleStatic(
        StaticSchedulingState state,
        int groupId,
        Func<ValueTask> work)
        => new(state.Handler.ScheduleTask(() => work().AsTask(), groupId, state.TypeToken));

    public static ValueTask<T> ScheduleStatic<T>(
        StaticSchedulingState state,
        int groupId,
        Func<ValueTask<T>> work)
        => new(state.Handler.ScheduleTask(() => work().AsTask(), groupId, state.TypeToken));

    public static ValueTask ScheduleStatic(
        StaticSchedulingState state,
        int groupId,
        Func<CancellationToken, ValueTask> work,
        CancellationToken methodToken)
    {
        var linked = state.CreateCallToken(methodToken);
        return new ValueTask(RunWithLinkedToken(state.Handler, groupId, linked, work));
    }

    public static ValueTask<T> ScheduleStatic<T>(
        StaticSchedulingState state,
        int groupId,
        Func<CancellationToken, ValueTask<T>> work,
        CancellationToken methodToken)
    {
        var linked = state.CreateCallToken(methodToken);
        return new ValueTask<T>(RunWithLinkedToken(state.Handler, groupId, linked, work));
    }

    public static void DisposeInstanceState(InstanceSchedulingState? state)
        => state?.Dispose();

    private static async Task RunWithLinkedToken(
        ActionsHandler handler,
        int groupId,
        CancellationTokenSource linked,
        Func<CancellationToken, Task> work)
    {
        try
        {
            await handler.ScheduleTask(() => work(linked.Token), groupId, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            linked.Dispose();
        }
    }

    private static async Task<T> RunWithLinkedToken<T>(
        ActionsHandler handler,
        int groupId,
        CancellationTokenSource linked,
        Func<CancellationToken, Task<T>> work)
    {
        try
        {
            return await handler.ScheduleTask(() => work(linked.Token), groupId, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            linked.Dispose();
        }
    }

    private static async Task RunWithLinkedToken(
        ActionsHandler handler,
        int groupId,
        CancellationTokenSource linked,
        Func<CancellationToken, ValueTask> work)
    {
        try
        {
            await handler.ScheduleTask(() => work(linked.Token).AsTask(), groupId, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            linked.Dispose();
        }
    }

    private static async Task<T> RunWithLinkedToken<T>(
        ActionsHandler handler,
        int groupId,
        CancellationTokenSource linked,
        Func<CancellationToken, ValueTask<T>> work)
    {
        try
        {
            return await handler.ScheduleTask(() => work(linked.Token).AsTask(), groupId, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            linked.Dispose();
        }
    }
}
