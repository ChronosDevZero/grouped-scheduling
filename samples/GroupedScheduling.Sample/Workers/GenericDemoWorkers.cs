using GroupedScheduling;

namespace GroupedScheduling.Sample;

/// <summary>Generic declaring type — common Repo&lt;T&gt; pattern.</summary>
public sealed class EntityStore<T>
{
    public List<string> Log { get; } = new();

    [ScheduleGroup("store", ScheduleMode.ExclusiveSerial)]
    public async Task SaveAsync(T item, string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START EntityStore<{typeof(T).Name}>.SaveAsync label={label}, item={item}");
        await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        Log.Add($"save:{label}:{item}");
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  EntityStore<{typeof(T).Name}>.SaveAsync");
    }

    [ScheduleGroup("store", ScheduleMode.ExclusiveSerial)]
    public async Task<T> EchoAsync(T item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START EntityStore<{typeof(T).Name}>.EchoAsync  item={item}");
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  EntityStore<{typeof(T).Name}>.EchoAsync  => {item}");
        return item;
    }

    [ScheduleGroup("store-boot", ScheduleMode.ExclusiveSerial)]
    public static async Task BootAsync()
    {
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START EntityStore<{typeof(T).Name}>.BootAsync (static)");
        Hits++;
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  EntityStore<{typeof(T).Name}>.BootAsync Hits={Hits}");
    }

    public static int Hits;
}

/// <summary>Generic scheduled methods on a non-generic host.</summary>
public sealed class GenericMethodHost
{
    public List<string> Log { get; } = new();

    [ScheduleGroup("gm", ScheduleMode.ExclusiveSerial)]
    public async Task AcceptAsync<T>(T item, string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START AcceptAsync<{typeof(T).Name}> label={label}, item={item}");
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        Log.Add($"accept:{label}:{item}");
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  AcceptAsync<{typeof(T).Name}>");
    }

    [ScheduleGroup("gm", ScheduleMode.ExclusiveSerial)]
    public async Task<T> IdentityAsync<T>(T item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START IdentityAsync<{typeof(T).Name}> item={item}");
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  IdentityAsync<{typeof(T).Name}> => {item}");
        return item;
    }

    [ScheduleGroup("gm", ScheduleMode.ExclusiveSerial)]
    public static async Task<int> CountAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = DateTime.Now;
        var list = items.ToList();
        Console.WriteLine($"[{start:HH:mm:ss.fff}] START CountAsync<{typeof(T).Name}> count={list.Count}");
        await Task.Yield();
        var done = DateTime.Now;
        Console.WriteLine($"[{done:HH:mm:ss.fff}] DONE  CountAsync<{typeof(T).Name}> => {list.Count}");
        return list.Count;
    }
}
