using GroupedScheduling;

namespace GroupedScheduling.Fixture.WeaveSubjects;

public class DisposableBase : IDisposable
{
    public bool BaseDisposed { get; private set; }

    public virtual void Dispose() => BaseDisposed = true;
}

public class SampleService : DisposableBase
{
    public List<string> Log { get; } = new();

    [ScheduleGroup("work", ScheduleMode.ExclusiveSerial)]
    public async Task WorkAsync()
    {
        Log.Add("work");
        await Task.Yield();
    }

    [ScheduleGroup("work", ScheduleMode.ExclusiveSerial)]
    public async Task NestedAsync()
    {
        Log.Add("nested-outer");
        await WorkAsync();
    }

    [ScheduleGroup("typed", ScheduleMode.ExclusiveSerial)]
    public async Task<int> GetValueAsync()
    {
        await Task.Yield();
        return 7;
    }

    [ScheduleGroup("vt", ScheduleMode.ExclusiveSerial)]
    public async ValueTask PingValueTaskAsync()
    {
        await Task.Yield();
    }

    [ScheduleGroup("vt", ScheduleMode.ExclusiveSerial)]
    public async ValueTask<int> GetValueTaskAsync()
    {
        await Task.Yield();
        return 11;
    }

    [ScheduleGroup("vt", ScheduleMode.ExclusiveSerial)]
    public async ValueTask SaveValueTaskAsync(string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log.Add($"vt-save:{label}");
        await Task.Yield();
    }

    [ScheduleGroup("cancelable", ScheduleMode.FullyConcurrent)]
    public async Task CancelableAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    [ScheduleGroup("args", ScheduleMode.ExclusiveSerial)]
    public async Task SaveAsync(string label, int value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log.Add($"save:{label}:{value}");
        await Task.Yield();
    }

    [ScheduleGroup("args", ScheduleMode.ExclusiveSerial)]
    public async Task<string> FormatAsync(string prefix, int n)
    {
        await Task.Yield();
        return $"{prefix}-{n}";
    }

    public sealed class NestedWorker
    {
        public List<string> Log { get; } = new();

        [ScheduleGroup("nested-type", ScheduleMode.ExclusiveSerial)]
        public async Task NestedWorkAsync(string tag)
        {
            Log.Add($"nested:{tag}");
            await Task.Yield();
        }
    }
}

/// <summary>Non-generic host with generic scheduled methods.</summary>
public sealed class GenericMethodHost
{
    public List<string> Log { get; } = new();

    [ScheduleGroup("gm", ScheduleMode.ExclusiveSerial)]
    public async Task AcceptAsync<T>(T item, string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log.Add($"accept:{label}:{item}");
        await Task.Yield();
    }

    [ScheduleGroup("gm", ScheduleMode.ExclusiveSerial)]
    public async Task<T> IdentityAsync<T>(T item)
    {
        await Task.Yield();
        return item;
    }

    [ScheduleGroup("gm", ScheduleMode.ExclusiveSerial)]
    public static async Task<int> CountAsync<T>(IEnumerable<T> items)
    {
        await Task.Yield();
        return items.Count();
    }
}

/// <summary>Generic declaring type — instance + static scheduled methods (common Repo&lt;T&gt; pattern).</summary>
public sealed class GenericStore<T>
{
    public List<string> Log { get; } = new();

    [ScheduleGroup("store", ScheduleMode.ExclusiveSerial)]
    public async Task SaveAsync(T item, string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log.Add($"save:{label}:{item}");
        await Task.Yield();
    }

    [ScheduleGroup("store", ScheduleMode.ExclusiveSerial)]
    public async Task<T> EchoAsync(T item)
    {
        await Task.Yield();
        return item;
    }

    [ScheduleGroup("store-boot", ScheduleMode.ExclusiveSerial)]
    public static async Task BootAsync()
    {
        Hits++;
        await Task.Yield();
    }

    public static int Hits;
}

public static class SampleStatic
{
    public static int Hits;

    [ScheduleGroup("boot", ScheduleMode.ExclusiveSerial)]
    public static async Task BootAsync()
    {
        Hits++;
        await Task.Yield();
    }

    [ScheduleGroup("boot", ScheduleMode.ExclusiveSerial)]
    public static async Task BootWithLabelAsync(string label)
    {
        Hits++;
        await Task.Yield();
        _ = label;
    }

    [ScheduleGroup("boot", ScheduleMode.ExclusiveSerial)]
    public static async ValueTask BootValueTaskAsync()
    {
        Hits++;
        await Task.Yield();
    }
}
