using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GroupedScheduling.Hosting;

/// <summary>
/// Host integration: one registration for process-level graceful stop.
/// </summary>
public static class GroupedSchedulingServiceCollectionExtensions
{
    /// <summary>
    /// Registers a hosted service that calls <see cref="HandlerRegistry.ShutdownAll"/> on application stop.
    /// Method authors still only need <c>[ScheduleGroup]</c>; callers need no per-call <c>using</c>.
    /// Object <see cref="IDisposable"/> (injected by the weaver) is still disposed by DI when the container disposes the instance.
    /// </summary>
    public static IServiceCollection AddGroupedScheduling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, GroupedSchedulingHostedService>());
        return services;
    }
}
