using GroupedScheduling;

namespace GroupedScheduling.Sample;

/// <summary>Generic declaring type + generic method: Mapper&lt;TIn&gt;.MapAsync&lt;TOut&gt;.</summary>
public sealed class MapperDemo<TIn>
{
    public int MapCount { get; private set; }

    [ScheduleGroup("map", ScheduleMode.ExclusiveSerial)]
    public async Task<TOut> MapAsync<TOut>(
        TIn input,
        Func<TIn, TOut> project,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine(
            $"[{start:HH:mm:ss.fff}] START Mapper<{typeof(TIn).Name}>.MapAsync<{typeof(TOut).Name}> input={input}");
        await Task.Yield();
        MapCount++;
        var result = project(input);
        var done = DateTime.Now;
        Console.WriteLine(
            $"[{done:HH:mm:ss.fff}] DONE  Mapper<{typeof(TIn).Name}>.MapAsync<{typeof(TOut).Name}> => {result}");
        return result;
    }
}
