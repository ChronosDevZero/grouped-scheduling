using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

public sealed class ArgsWorker : IDisposable
{
    public string? LastSave { get; private set; }

    public void Dispose() { }
    public string? LastTyped { get; private set; }
    public string? LastMidCt { get; private set; }

    [ScheduleGroup("args", ScheduleMode.ExclusiveSerial)]
    public async Task SaveAsync(string label, int value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastSave = $"{label}:{value}";
        await Task.Yield();
    }

    [ScheduleGroup("args", ScheduleMode.ExclusiveSerial)]
    public async Task<string> FormatAsync(string prefix, int n)
    {
        await Task.Yield();
        LastTyped = $"{prefix}-{n}";
        return LastTyped;
    }

    [ScheduleGroup("args", ScheduleMode.ExclusiveSerial)]
    public async Task MidTokenAsync(string label, CancellationToken cancellationToken, int n)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastMidCt = $"{label}:{n}";
        await Task.Yield();
    }
}
