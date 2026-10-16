using Xunit;

namespace GroupedScheduling.Tests.Engine;

public class NestingTests
{
    private static ActionsHandler Create(params (int id, ScheduleMode mode)[] groups)
    {
        var map = groups.ToDictionary(
            g => g.id,
            g => new EventActionGroup($"g{g.id}", g.mode));
        return new ActionsHandler(map);
    }

    [Fact]
    public async Task SameHandler_NestedSchedule_Throws()
    {
        var handler = Create((1, ScheduleMode.FullyConcurrent), (2, ScheduleMode.FullyConcurrent));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ScheduleTask(async () =>
            {
                await handler.ScheduleTask(() => Task.CompletedTask, 2);
            }, 1));

        Assert.Contains("Nested scheduled call", ex.Message);
    }

    [Fact]
    public async Task DifferentHandlers_AtoB_DoesNotThrow()
    {
        var a = Create((1, ScheduleMode.FullyConcurrent));
        var b = Create((1, ScheduleMode.FullyConcurrent));
        var ran = false;

        await a.ScheduleTask(async () =>
        {
            await b.ScheduleTask(() =>
            {
                ran = true;
                return Task.CompletedTask;
            }, 1);
        }, 1);

        Assert.True(ran);
    }

    [Fact]
    public async Task A_to_B_to_A_Throws()
    {
        var a = Create((1, ScheduleMode.FullyConcurrent));
        var b = Create((1, ScheduleMode.FullyConcurrent));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            a.ScheduleTask(async () =>
            {
                await b.ScheduleTask(async () =>
                {
                    await a.ScheduleTask(() => Task.CompletedTask, 1);
                }, 1);
            }, 1));
    }

    [Fact]
    public async Task AfterOuterCompletes_SameHandler_CanRunAgain()
    {
        var handler = Create((1, ScheduleMode.ExclusiveSerial));

        await handler.ScheduleTask(() => Task.CompletedTask, 1);
        await handler.ScheduleTask(() => Task.CompletedTask, 1);
    }
}
