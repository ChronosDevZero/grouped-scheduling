using Microsoft.Extensions.Hosting;

namespace GroupedScheduling.Hosting;

/// <summary>
/// Shuts down all tracked scheduling handlers when the host stops.
/// </summary>
internal sealed class GroupedSchedulingHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
        => HandlerRegistry.ShutdownAllAsync(drainTimeout: TimeSpan.FromSeconds(30));
}
