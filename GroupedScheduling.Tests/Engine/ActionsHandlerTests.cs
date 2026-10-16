using Xunit;

namespace GroupedScheduling.Tests.Engine;

public class ActionsHandlerTests
{
    private static ActionsHandler CreateHandler(params (int id, EventActionGroup group)[] groups)
    {
        var map = groups.ToDictionary(g => g.id, g => g.group);
        return new ActionsHandler(map);
    }

    [Fact]
    public async Task ExclusiveSerial_RunsTasksOneAfterAnother_InSameGroup()
    {
        var group = new EventActionGroup("serial", ScheduleMode.ExclusiveSerial);
        var handler = CreateHandler((1, group));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<int>();

        var first = handler.ScheduleTask(async () =>
        {
            order.Add(1);
            await gate.Task;
            order.Add(2);
        }, 1);

        // Give the first task time to start.
        await Task.Delay(50);

        var secondStarted = false;
        var second = handler.ScheduleTask(async () =>
        {
            secondStarted = true;
            order.Add(3);
            await Task.Yield();
        }, 1);

        await Task.Delay(50);
        Assert.False(secondStarted);

        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public async Task WaitingGroups_ActivateInFifoOrder()
    {
        var a = new EventActionGroup("a", ScheduleMode.ExclusiveSerial);
        var b = new EventActionGroup("b", ScheduleMode.ExclusiveSerial);
        var c = new EventActionGroup("c", ScheduleMode.ExclusiveSerial);
        var handler = CreateHandler((1, a), (2, b), (3, c));

        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var lockObj = new object();

        void Record(string name)
        {
            lock (lockObj)
                order.Add(name);
        }

        var taskA = handler.ScheduleTask(async () =>
        {
            Record("a-start");
            await releaseA.Task;
            Record("a-end");
        }, 1);

        await Task.Delay(30);

        var taskB = handler.ScheduleTask(async () =>
        {
            Record("b");
            await Task.Yield();
        }, 2);

        var taskC = handler.ScheduleTask(async () =>
        {
            Record("c");
            await Task.Yield();
        }, 3);

        await Task.Delay(30);
        releaseA.SetResult();
        await Task.WhenAll(taskA, taskB, taskC);

        Assert.Equal(new[] { "a-start", "a-end", "b", "c" }, order);
    }

    [Fact]
    public async Task FullyConcurrent_DoesNotWaitOnExclusiveGroup()
    {
        var exclusive = new EventActionGroup("ex", ScheduleMode.ExclusiveSerial);
        var concurrent = new EventActionGroup("go", ScheduleMode.FullyConcurrent);
        var handler = CreateHandler((1, exclusive), (2, concurrent));

        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrentRan = false;

        var blocked = handler.ScheduleTask(async () => await hold.Task, 1);
        await Task.Delay(20);

        await handler.ScheduleTask(() =>
        {
            concurrentRan = true;
            return Task.CompletedTask;
        }, 2);

        Assert.True(concurrentRan);
        hold.SetResult();
        await blocked;
    }

    [Fact]
    public async Task ScheduleTask_PropagatesException()
    {
        var group = new EventActionGroup("ex", ScheduleMode.ExclusiveSerial);
        var handler = CreateHandler((1, group));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ScheduleTask(
                () => Task.FromException(new InvalidOperationException("boom")),
                1));

        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task ScheduleTaskOfT_ReturnsResult_AndPropagatesException()
    {
        var group = new EventActionGroup("ex", ScheduleMode.ExclusiveSerial);
        var handler = CreateHandler((1, group));

        var value = await handler.ScheduleTask(async () =>
        {
            await Task.Yield();
            return 42;
        }, 1);

        Assert.Equal(42, value);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ScheduleTask<int>(
                () => Task.FromException<int>(new InvalidOperationException("typed")),
                1));
    }

    [Fact]
    public async Task Shutdown_FaultsNewWork_BothOverloads()
    {
        var group = new EventActionGroup("ex", ScheduleMode.ExclusiveSerial);
        var handler = CreateHandler((1, group));
        await handler.ShutdownAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ScheduleTask(() => Task.CompletedTask, 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ScheduleTask(() => Task.FromResult(1), 1));
    }

    [Fact]
    public async Task CancelWhileQueued_FaultsCaller_AndDoesNotStartWork()
    {
        var a = new EventActionGroup("a", ScheduleMode.ExclusiveSerial);
        var b = new EventActionGroup("b", ScheduleMode.ExclusiveSerial);
        var handler = CreateHandler((1, a), (2, b));
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bStarted = false;

        var first = handler.ScheduleTask(async () => await hold.Task, 1);
        using var cts = new CancellationTokenSource();
        var second = handler.ScheduleTask(() =>
        {
            bStarted = true;
            return Task.CompletedTask;
        }, 2, cts.Token);

        await Task.Delay(30);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await second);
        Assert.False(bStarted);

        hold.SetResult();
        await first;
    }

    [Fact]
    public async Task ExclusiveParallel_AllowsOverlapInGroup()
    {
        var group = new EventActionGroup("par", ScheduleMode.ExclusiveParallel);
        var handler = CreateHandler((1, group));

        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var t1 = handler.ScheduleTask(async () =>
        {
            firstEntered.TrySetResult();
            await release.Task;
        }, 1);

        var t2 = handler.ScheduleTask(async () =>
        {
            secondEntered.TrySetResult();
            await release.Task;
        }, 1);

        await Task.WhenAll(firstEntered.Task, secondEntered.Task).WaitAsync(TimeSpan.FromSeconds(2));

        release.SetResult();
        await Task.WhenAll(t1, t2);
    }

    [Fact]
    public async Task SerialIntervals_NeverOverlap()
    {
        var handler = CreateHandler((1, new EventActionGroup("s", ScheduleMode.ExclusiveSerial)));
        var intervals = new List<(long Start, long End)>();
        var gate = new object();

        var tasks = Enumerable.Range(0, 12).Select(_ => handler.ScheduleTask(async () =>
        {
            var start = Environment.TickCount64;
            await Task.Delay(15);
            var end = Environment.TickCount64;
            lock (gate) intervals.Add((start, end));
        }, 1)).ToArray();

        await Task.WhenAll(tasks);
        var ordered = intervals.OrderBy(i => i.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.True(ordered[i].Start >= ordered[i - 1].End, "ExclusiveSerial intervals overlapped");
    }
}
