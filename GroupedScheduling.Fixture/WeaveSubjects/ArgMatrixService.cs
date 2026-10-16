using GroupedScheduling;

namespace GroupedScheduling.Fixture.WeaveSubjects;

/// <summary>Signature matrix for weaver argument-capture tests.</summary>
public class ArgMatrixService
{
    public List<string> Log { get; } = new();

    [ScheduleGroup("m", ScheduleMode.ExclusiveSerial)]
    public async Task ZeroAsync()
    {
        Log.Add("zero");
        await Task.Yield();
    }

    [ScheduleGroup("m", ScheduleMode.ExclusiveSerial)]
    public async Task OnlyTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log.Add("only-token");
        await Task.Yield();
    }

    [ScheduleGroup("m", ScheduleMode.ExclusiveSerial)]
    public async Task ManyValueAsync(int a, int b, int c, int d, int e, int f, int g, int h)
    {
        Log.Add($"vals:{a}{b}{c}{d}{e}{f}{g}{h}");
        await Task.Yield();
    }

    [ScheduleGroup("m", ScheduleMode.ExclusiveSerial)]
    public async Task ManyRefAsync(string a, string b, string c, object o)
    {
        Log.Add($"refs:{a}:{b}:{c}:{o}");
        await Task.Yield();
    }

    [ScheduleGroup("m", ScheduleMode.ExclusiveSerial)]
    public async Task<string> TypedWithArgsAsync(string prefix, int n, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return $"{prefix}:{n}";
    }

    [ScheduleGroup("m", ScheduleMode.ExclusiveSerial)]
    public async Task TokenNotLastAsync(string label, CancellationToken cancellationToken, int n)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log.Add($"mid-ct:{label}:{n}");
        await Task.Yield();
    }

    [ScheduleGroup("c", ScheduleMode.FullyConcurrent)]
    public async Task ConcurrentWithArgsAsync(string tag)
    {
        Log.Add($"conc:{tag}");
        await Task.Yield();
    }
}

public class MultiCtorService
{
    public string CtorPath { get; }
    public List<string> Log { get; } = new();

    public MultiCtorService() => CtorPath = "default";

    public MultiCtorService(string marker)
    {
        CtorPath = marker;
    }

    public MultiCtorService(string marker, int _) : this(marker)
    {
    }

    [ScheduleGroup("mc", ScheduleMode.ExclusiveSerial)]
    public async Task WorkAsync(string tag)
    {
        Log.Add($"{CtorPath}:{tag}");
        await Task.Yield();
    }
}

public class OuterNest
{
    public class InnerNest
    {
        public class DeeperNest
        {
            public List<string> Log { get; } = new();

            [ScheduleGroup("deep", ScheduleMode.ExclusiveSerial)]
            public async Task DeepAsync(string tag)
            {
                Log.Add($"deep:{tag}");
                await Task.Yield();
            }
        }
    }
}

public class OwnDisposeService : IDisposable
{
    public bool OwnDisposeCalled { get; private set; }
    public List<string> Log { get; } = new();

    [ScheduleGroup("od", ScheduleMode.ExclusiveSerial)]
    public async Task WorkAsync()
    {
        Log.Add("od-work");
        await Task.Yield();
    }

    public void Dispose() => OwnDisposeCalled = true;
}
