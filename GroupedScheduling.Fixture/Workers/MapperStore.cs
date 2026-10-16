using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

/// <summary>Generic declaring type + generic method (Mapper&lt;TIn&gt;.MapAsync&lt;TOut&gt;).</summary>
public sealed class MapperStore<TIn> : IDisposable
{
    public int MapCount { get; private set; }

    public void Dispose() { }

    [ScheduleGroup("map", ScheduleMode.ExclusiveSerial)]
    public async Task<TOut> MapAsync<TOut>(
        TIn input,
        Func<TIn, TOut> project,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        MapCount++;
        return project(input);
    }
}
