using Xunit;

namespace GroupedScheduling.Tests.Engine;

public class ShutdownTests
{
    [Fact]
    public async Task ObjectTokenCancel_CancelsLinkedCallToken()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.FullyConcurrent),
        };
        var state = new InstanceSchedulingState(map);
        HandlerRegistry.GetOrCreateInstanceState(state, () => state);

        using var linked = state.CreateCallToken();
        Assert.False(linked.IsCancellationRequested);

        state.Shutdown();
        Assert.True(linked.IsCancellationRequested);
        Assert.True(state.Handler.IsShuttingDown);
    }

    [Fact]
    public async Task LinkedToken_AlsoCancelsWhenMethodTokenCancels()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.FullyConcurrent),
        };
        var state = new InstanceSchedulingState(map);
        using var methodCts = new CancellationTokenSource();
        using var linked = state.CreateCallToken(methodCts.Token);

        methodCts.Cancel();
        Assert.True(linked.IsCancellationRequested);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Shutdown_BlocksNewGenericWork()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.ExclusiveSerial),
        };
        var state = new InstanceSchedulingState(map);
        state.Shutdown();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            state.Handler.ScheduleTask(() => Task.FromResult(1), 1));
    }

    [Fact]
    public void StaticState_Shutdown_CancelsTypeToken()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.FullyConcurrent),
        };
        var state = HandlerRegistry.GetOrCreateStaticState(typeof(ShutdownTests), () => new StaticSchedulingState(map));
        using var linked = state.CreateCallToken();
        state.Shutdown();
        Assert.True(linked.IsCancellationRequested);
    }

    [Fact]
    public async Task ShutdownAsync_MidRun_CancelsQueuedExclusive_AndRejectsNewWork()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("a", ScheduleMode.ExclusiveSerial),
            [2] = new EventActionGroup("b", ScheduleMode.ExclusiveSerial),
        };
        var handler = new ActionsHandler(map);
        using var holdCts = new CancellationTokenSource();

        // Hold the exclusive lane; Delay observes holdCts (cancelled when shutdown starts so drain can finish).
        var holder = handler.ScheduleTask(
            () => Task.Delay(Timeout.Infinite, holdCts.Token),
            1,
            holdCts.Token);
        await Task.Delay(40);

        var queuedStarted = false;
        var queued = handler.ScheduleTask(async () =>
        {
            queuedStarted = true;
            await Task.Yield();
        }, 2);
        await Task.Delay(40);
        Assert.False(queuedStarted, "queued exclusive B must wait behind A");

        var shutdown = handler.ShutdownAsync(drainTimeout: TimeSpan.FromSeconds(3));
        holdCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await queued);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await holder);
        await shutdown;

        Assert.False(queuedStarted);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ScheduleTask(() => Task.CompletedTask, 1));
    }

    // Note: do not call HandlerRegistry.ShutdownAllAsync here — it would cancel other
    // parallel xUnit workers that share the process-wide registry. Sample HostStopSmoke covers that.
}
