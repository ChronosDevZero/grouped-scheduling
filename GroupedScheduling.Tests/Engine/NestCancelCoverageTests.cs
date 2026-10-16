using GroupedScheduling.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GroupedScheduling.Tests.Engine;

public class NestCancelCoverageTests
{
    private static ActionsHandler Handler(params (int id, ScheduleMode mode)[] groups)
        => new(groups.ToDictionary(g => g.id, g => new EventActionGroup($"g{g.id}", g.mode)));

    [Fact]
    public async Task DeepCrossHandler_A_B_C_Ok()
    {
        var a = Handler((1, ScheduleMode.FullyConcurrent));
        var b = Handler((1, ScheduleMode.FullyConcurrent));
        var c = Handler((1, ScheduleMode.FullyConcurrent));
        var leaf = false;

        await a.ScheduleTask(async () =>
        {
            await b.ScheduleTask(async () =>
            {
                await c.ScheduleTask(() =>
                {
                    leaf = true;
                    return Task.CompletedTask;
                }, 1);
            }, 1);
        }, 1);

        Assert.True(leaf);
    }

    [Fact]
    public async Task FireAndForget_NestedStillDetected()
    {
        var handler = Handler((1, ScheduleMode.FullyConcurrent), (2, ScheduleMode.FullyConcurrent));
        Exception? nestedEx = null;

        await handler.ScheduleTask(async () =>
        {
            try
            {
                // Don't await — nest stack still active on same handler.
                await handler.ScheduleTask(() => Task.CompletedTask, 2);
            }
            catch (Exception ex)
            {
                nestedEx = ex;
            }
        }, 1);

        Assert.NotNull(nestedEx);
        Assert.Contains("Nested scheduled call", nestedEx!.Message);
    }

    [Fact]
    public async Task ObjectToken_CancelsQueuedExclusiveWaiter()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("a", ScheduleMode.ExclusiveSerial),
            [2] = new EventActionGroup("b", ScheduleMode.ExclusiveSerial),
        };
        var state = new InstanceSchedulingState(map);
        HandlerRegistry.GetOrCreateInstanceState(state, () => state);

        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Hold the exclusive lane without observing ObjectToken so shutdown can cancel waiters
        // without deadlocking on an infinite drain of this in-flight work.
        var first = state.Handler.ScheduleTask(async () => await hold.Task, 1);
        var queuedStarted = false;
        var queued = state.Handler.ScheduleTask(() =>
        {
            queuedStarted = true;
            return Task.CompletedTask;
        }, 2, state.ObjectToken);

        await Task.Delay(30);
        var shuttingDown = state.ShutdownAsync(drainTimeout: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await queued);
        Assert.False(queuedStarted);
        hold.SetResult();
        await first;
        await shuttingDown;
    }

    [Fact]
    public async Task TokenLess_InFlight_CompletesAfterShutdown()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.ExclusiveSerial),
        };
        var state = new InstanceSchedulingState(map);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var work = state.Handler.ScheduleTask(async () =>
        {
            started.TrySetResult();
            await release.Task;
        }, 1); // no CT — shutdown cannot stop body

        await started.Task;
        // Timed drain so we do not block forever on token-less in-flight work.
        var shutdown = state.ShutdownAsync(TimeSpan.FromMilliseconds(30));
        await Task.Delay(50);
        release.SetResult();
        await work;
        await shutdown.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DoubleDispose_IsSafe()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.FullyConcurrent),
        };
        var state = new InstanceSchedulingState(map);
        state.Dispose();
        state.Dispose();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            state.Handler.ScheduleTask(() => Task.CompletedTask, 1));
    }

    [Fact]
    public async Task NewInstance_AfterPreviousShutdown_Works()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.ExclusiveSerial),
        };
        var a = new InstanceSchedulingState(map);
        a.Shutdown();

        var b = new InstanceSchedulingState(new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.ExclusiveSerial),
        });
        await b.Handler.ScheduleTask(() => Task.CompletedTask, 1);
        b.Dispose();
    }

    [Fact]
    public async Task MethodTokenOnly_CancelsLinked_ObjectAlive()
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
        Assert.False(state.ObjectToken.IsCancellationRequested);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task AddGroupedScheduling_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddGroupedScheduling();
        services.AddGroupedScheduling();
        var hosted = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
        Assert.Single(hosted);
        await Task.CompletedTask;
    }
}
