using Xunit;

namespace GroupedScheduling.Tests.Engine;

/// <summary>Extra engine scenarios from the 100-idea matrix (modes, FIFO, exceptions, reject).</summary>
public class EngineCoverageTests
{
    private static ActionsHandler Create(params (int id, string name, ScheduleMode mode)[] groups)
        => new(groups.ToDictionary(g => g.id, g => new EventActionGroup(g.name, g.mode)));

    [Fact]
    public void EmptyGroupName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new EventActionGroup(" ", ScheduleMode.ExclusiveSerial));
        Assert.Throws<ArgumentException>(() => new ScheduleGroupAttribute("", ScheduleMode.FullyConcurrent));
    }

    [Fact]
    public async Task UnknownGroupId_Throws()
    {
        var handler = Create((1, "a", ScheduleMode.ExclusiveSerial));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.ScheduleTask(() => Task.CompletedTask, 99));
    }

    [Fact]
    public async Task TwoExclusiveParallelGroups_DoNotOverlap()
    {
        var handler = Create(
            (1, "p1", ScheduleMode.ExclusiveParallel),
            (2, "p2", ScheduleMode.ExclusiveParallel));

        var p1Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var p2Started = false;

        var t1 = handler.ScheduleTask(async () => await p1Hold.Task, 1);
        await Task.Delay(30);
        var t2 = handler.ScheduleTask(() =>
        {
            p2Started = true;
            return Task.CompletedTask;
        }, 2);

        await Task.Delay(40);
        Assert.False(p2Started);
        p1Hold.SetResult();
        await Task.WhenAll(t1, t2);
        Assert.True(p2Started);
    }

    [Fact]
    public async Task ParallelException_DoesNotStallSibling()
    {
        var handler = Create((1, "p", ScheduleMode.ExclusiveParallel));
        var siblingDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var boom = handler.ScheduleTask(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        }, 1);

        var ok = handler.ScheduleTask(async () =>
        {
            await Task.Delay(20);
            siblingDone.TrySetResult();
        }, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await boom);
        await siblingDone.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await ok;
    }

    [Fact]
    public async Task Exception_FreesLaneForNextGroup()
    {
        var handler = Create(
            (1, "a", ScheduleMode.ExclusiveSerial),
            (2, "b", ScheduleMode.ExclusiveSerial));

        var bRan = false;
        var fail = handler.ScheduleTask(() => Task.FromException(new InvalidOperationException("x")), 1);
        var next = handler.ScheduleTask(() =>
        {
            bRan = true;
            return Task.CompletedTask;
        }, 2);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fail);
        await next;
        Assert.True(bRan);
    }

    [Fact]
    public async Task ZeroDuration_SerialStillOrdered()
    {
        var handler = Create((1, "s", ScheduleMode.ExclusiveSerial));
        var order = new List<int>();
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 20).Select(i =>
            handler.ScheduleTask(() =>
            {
                lock (lockObj) order.Add(i);
                return Task.CompletedTask;
            }, 1)).ToArray();

        await Task.WhenAll(tasks);
        Assert.Equal(Enumerable.Range(0, 20), order);
    }

    [Fact]
    public async Task FullyConcurrent_TaskOfT()
    {
        var handler = Create((1, "c", ScheduleMode.FullyConcurrent));
        var n = await handler.ScheduleTask(async () =>
        {
            await Task.Yield();
            return 11;
        }, 1);
        Assert.Equal(11, n);
    }

    [Fact]
    public async Task Dispose_RejectsNewWork()
    {
        var handler = Create((1, "s", ScheduleMode.ExclusiveSerial));
        handler.Dispose();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ScheduleTask(() => Task.CompletedTask, 1));
    }

    [Fact]
    public async Task Burst_MixedModes_Completes()
    {
        var handler = Create(
            (1, "s", ScheduleMode.ExclusiveSerial),
            (2, "p", ScheduleMode.ExclusiveParallel),
            (3, "c", ScheduleMode.FullyConcurrent));

        var tasks = new List<Task>();
        for (var i = 0; i < 40; i++)
        {
            var id = (i % 3) + 1;
            tasks.Add(handler.ScheduleTask(async () => await Task.Yield(), id));
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ManyInstances_IndependentHandlers()
    {
        var tasks = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            var handler = Create((1, "s", ScheduleMode.ExclusiveSerial));
            tasks.Add(handler.ScheduleTask(async () => await Task.Yield(), 1));
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Fifo_ManyWaiters_DrainInEnqueueOrder()
    {
        var handler = Create(
            (1, "hold", ScheduleMode.ExclusiveSerial),
            (2, "w0", ScheduleMode.ExclusiveSerial),
            (3, "w1", ScheduleMode.ExclusiveSerial),
            (4, "w2", ScheduleMode.ExclusiveSerial),
            (5, "w3", ScheduleMode.ExclusiveSerial));

        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<int>();
        var gate = new object();

        var blocker = handler.ScheduleTask(async () => await hold.Task, 1);
        await Task.Delay(20);

        var waiters = new List<Task>();
        for (var id = 2; id <= 5; id++)
        {
            var captured = id;
            waiters.Add(handler.ScheduleTask(() =>
            {
                lock (gate) order.Add(captured);
                return Task.CompletedTask;
            }, captured));
        }

        await Task.Delay(20);
        hold.SetResult();
        await blocker;
        await Task.WhenAll(waiters);
        Assert.Equal(new[] { 2, 3, 4, 5 }, order);
    }
}
