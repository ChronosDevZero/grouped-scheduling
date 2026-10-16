using GroupedScheduling.Fixture.Workers;
using Xunit;

namespace GroupedScheduling.Tests.Workers;

/// <summary>
/// Behavior tests against real [ScheduleGroup] workers (MSBuild-woven Fixture assembly).
/// </summary>
public class WorkerBehaviorTests
{
    [Fact]
    public async Task SerialWorker_IntervalsNeverOverlap()
    {
        var w = new SerialWorker();
        await Task.WhenAll(w.S1Async(), w.S2Async(), w.S3Async(), w.S4Async(),
            w.S1Async(), w.S2Async(), w.S3Async(), w.S4Async());

        IntervalRecorder.AssertNoOverlap(w.Recorder.Snapshot(), "serial");
        Assert.True(w.Recorder.Snapshot().Count(i => i.Group == "serial") >= 8);
    }

    [Fact]
    public async Task ParallelWorker_SameGroupOverlaps()
    {
        var w = new ParallelWorker();
        await Task.WhenAll(w.P1Async(), w.P2Async(), w.P3Async());
        IntervalRecorder.AssertSomeOverlap(w.Recorder.Snapshot(), "par");
    }

    [Fact]
    public async Task MapperStore_GenericTypePlusGenericMethod_Works()
    {
        var mapper = new MapperStore<int>();
        var text = await mapper.MapAsync(42, n => $"n={n}");
        Assert.Equal("n=42", text);
        Assert.Equal(1, mapper.MapCount);

        var doubled = await mapper.MapAsync(21, n => n * 2);
        Assert.Equal(42, doubled);
        Assert.Equal(2, mapper.MapCount);
    }

    [Fact]
    public async Task LaneWorker_ExclusiveGroupsDoNotOverlap_AnytimeBypasses()
    {
        var w = new LaneWorker();
        var holdA = w.ExAAsync(80);
        await Task.Delay(15);
        var anytime = w.AnytimeAsync(20);
        var b = w.ExBAsync(20);

        await Task.WhenAll(holdA, anytime, b);
        var snap = w.Recorder.Snapshot();

        var a = snap.Single(i => i.Name == "ExA");
        var exB = snap.Single(i => i.Name == "ExB");
        var any = snap.Single(i => i.Name == "Anytime");

        Assert.False(IntervalRecorder.Intersects(a, exB), "ex-a and ex-b must not share the exclusive lane");
        Assert.True(IntervalRecorder.Intersects(a, any),
            "anytime (FullyConcurrent) must overlap exclusive A — lane bypass");
        Assert.True(exB.Start >= a.End, "ex-b must wait until ex-a finishes");
    }

    [Fact]
    public async Task ArgsWorker_BusinessParamsReachBody()
    {
        var w = new ArgsWorker();
        await w.SaveAsync("order", 7);
        Assert.Equal("order:7", w.LastSave);

        var typed = await w.FormatAsync("pfx", 3);
        Assert.Equal("pfx-3", typed);
        Assert.Equal("pfx-3", w.LastTyped);

        await w.MidTokenAsync("mid", CancellationToken.None, 9);
        Assert.Equal("mid:9", w.LastMidCt);
    }

    [Fact]
    public async Task ExceptionWorker_Propagates_AndFreesLane()
    {
        var w = new ExceptionWorker();
        var boomTask = w.BoomAsync();
        var nextTask = w.NextAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await boomTask);
        Assert.Equal("woven-boom", ex.Message);
        await nextTask;
        Assert.True(w.SiblingRan);
    }

    [Fact]
    public async Task NestWorker_SameInstance_Throws()
    {
        var w = new NestWorker();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => w.OuterAsync());
        Assert.Contains("Nested scheduled call", ex.Message);
    }

    [Fact]
    public async Task NestWorker_CrossInstance_AtoB_Succeeds()
    {
        var a = new NestWorker();
        var b = new NestWorker();
        a.Peer = b;

        await a.CallPeerAsync();
        Assert.True(b.PeerWorkRan);
    }

    [Fact]
    public async Task CancelWorker_MethodToken_CancelsInFlight()
    {
        var w = new CancelWorker();
        using var cts = new CancellationTokenSource();
        var hold = w.HoldAsync(10_000, cts.Token);
        await Task.Delay(30);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await hold);
    }

    [Fact]
    public async Task StaticWorker_ArgsAndHits()
    {
        StaticWorker.Hits = 0;
        StaticWorker.LastLabel = null;
        await StaticWorker.HitAsync();
        await StaticWorker.HitWithLabelAsync("lbl");
        Assert.True(StaticWorker.Hits >= 2);
        Assert.Equal("lbl", StaticWorker.LastLabel);

        var n = await StaticWorker.CountAsync([1, 2, 3, 4]);
        Assert.Equal(4, n);
        Assert.True(StaticWorker.Hits >= 2);
    }

    [Fact]
    public async Task LaneWorker_Dispose_CancelsInFlightAndQueued()
    {
        var w = new LaneWorker();
        var blocker = w.ExAAsync(5_000);
        await Task.Delay(20);
        var queued = w.ExBAsync(20);
        await Task.Delay(20);
        w.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await queued);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await blocker);
    }
}
