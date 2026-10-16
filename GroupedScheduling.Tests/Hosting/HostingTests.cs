using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GroupedScheduling.Tests.Hosting;

/// <summary>
/// Host StopAsync / HandlerRegistry.ShutdownAll is covered by the Sample process smoke
/// (so this suite can run in parallel). Here we only keep DI dispose of a single owner.
/// </summary>
public class HostingTests
{
    [Fact]
    public async Task DiDispose_ShutsDownSingleInstanceState()
    {
        var map = new Dictionary<int, EventActionGroup>
        {
            [1] = new EventActionGroup("g", ScheduleMode.FullyConcurrent),
        };

        var services = new ServiceCollection();
        services.AddSingleton(_ =>
        {
            var s = new InstanceSchedulingState(map);
            return new DisposableOwner(s);
        });
        await using var provider = services.BuildServiceProvider();
        var owner = provider.GetRequiredService<DisposableOwner>();
        using var linked = owner.State.CreateCallToken();

        await provider.DisposeAsync();

        Assert.True(linked.IsCancellationRequested);
        Assert.True(owner.State.Handler.IsShuttingDown);
    }

    private sealed class DisposableOwner : IDisposable
    {
        public InstanceSchedulingState State { get; }
        public DisposableOwner(InstanceSchedulingState state) => State = state;
        public void Dispose() => State.Dispose();
    }
}
