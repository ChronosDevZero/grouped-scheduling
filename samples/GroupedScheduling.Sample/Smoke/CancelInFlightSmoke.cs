namespace GroupedScheduling.Sample.Smoke;

internal static class CancelInFlightSmoke
{
    public static async Task RunAsync(LifecycleDemoWorker worker)
    {
        Console.WriteLine();
        Console.WriteLine("===== CANCEL IN FLIGHT (method token) =====");

        using var cts = new CancellationTokenSource();
        var hold = worker.HoldAsync(10_000, cts.Token);
        await Task.Delay(50);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] cancel method token");
        cts.Cancel();

        try
        {
            await hold;
            throw new InvalidOperationException("Expected OperationCanceledException after cancel.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            Console.WriteLine($"  hold failed as expected ({ex.GetType().Name})");
        }

        Console.WriteLine("ASSERT OK: in-flight Delay observed CancellationToken.");
        Console.WriteLine("------------------");
    }
}
