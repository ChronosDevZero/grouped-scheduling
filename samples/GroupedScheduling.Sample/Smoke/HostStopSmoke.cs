using Microsoft.Extensions.Hosting;

namespace GroupedScheduling.Sample.Smoke;

internal static class HostStopSmoke
{
    public static async Task RunAsync(
        IHost host,
        ModesDemoWorker worker,
        AuxWorker aux,
        ValueTaskDemoWorker valueTasks,
        EntityStore<int> intStore,
        GenericMethodHost genericMethods,
        LifecycleDemoWorker life,
        MapperDemo<int> mapper)
    {
        Console.WriteLine();
        Console.WriteLine("===== MID-RUN STOP (queued exclusive + drain) =====");

        using var holdCts = new CancellationTokenSource();
        var holder = life.DrainHoldAsync(Timeout.Infinite, holdCts.Token);
        await Task.Delay(40);
        var queued = life.DrainQueuedAsync();
        await Task.Delay(40);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] host.StopAsync (ShutdownAll) while hold + queued");

        var stop = host.StopAsync();
        // In-flight Delay must observe cancel so drain can complete (same pattern as woven ObjectToken).
        holdCts.Cancel();

        await ExpectCancel(holder, "DrainHoldAsync");
        await ExpectCancel(queued, "DrainQueuedAsync");
        await stop;
        Console.WriteLine("ASSERT OK: mid-run stop cancelled hold + queued drain work.");

        Console.WriteLine();
        Console.WriteLine("===== POST-STOP (new work rejected) =====");
        await ExpectPostStopFailure(() => worker.Serial1Async(), "ModesDemoWorker");
        await ExpectPostStopFailure(() => aux.PingAsync(), "AuxWorker");
        await ExpectPostStopFailure(async () => await valueTasks.PingAsync("after-stop"), "ValueTaskDemoWorker");
        await ExpectPostStopFailure(() => intStore.SaveAsync(1, "after-stop"), "EntityStore<int>");
        await ExpectPostStopFailure(() => genericMethods.AcceptAsync(1, "after-stop"), "GenericMethodHost");
        await ExpectPostStopFailure(() => life.HoldAsync(10), "LifecycleDemoWorker");
        await ExpectPostStopFailure(() => mapper.MapAsync(1, n => n), "MapperDemo<int>");
        await ExpectPostStopFailure(() => StaticOnlyJobs.BootAsync(), "StaticOnlyJobs");
        Console.WriteLine("ASSERT OK: post-stop work failed on all demo workers.");
    }

    private static async Task ExpectCancel(Task work, string name)
    {
        try
        {
            await work;
            throw new InvalidOperationException($"{name}: expected cancel after mid-run stop.");
        }
        catch (Exception ex) when (ex is OperationCanceledException
                                           or TaskCanceledException
                                           or ObjectDisposedException
                                           or InvalidOperationException)
        {
            Console.WriteLine($"  {name}: failed as expected ({ex.GetType().Name})");
        }
    }

    private static async Task ExpectPostStopFailure(Func<Task> work, string name)
    {
        try
        {
            await work();
            throw new InvalidOperationException($"{name}: expected failure after host stop, but work completed.");
        }
        catch (Exception ex) when (ex is ObjectDisposedException
                                           or InvalidOperationException
                                           or OperationCanceledException
                                           or TaskCanceledException)
        {
            Console.WriteLine($"  {name}: failed as expected ({ex.GetType().Name})");
        }
    }
}
