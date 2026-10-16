using GroupedScheduling;

namespace GroupedScheduling.Fixture.Workers;

public sealed class ExceptionWorker : IDisposable
{
    public bool SiblingRan { get; private set; }

    public void Dispose() { }

    [ScheduleGroup("boom", ScheduleMode.ExclusiveSerial)]
    public async Task BoomAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("woven-boom");
    }

    [ScheduleGroup("next", ScheduleMode.ExclusiveSerial)]
    public async Task NextAsync()
    {
        SiblingRan = true;
        await Task.Yield();
    }
}
