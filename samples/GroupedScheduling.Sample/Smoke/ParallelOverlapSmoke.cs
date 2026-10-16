namespace GroupedScheduling.Sample.Smoke;

internal static class ParallelOverlapSmoke
{
    public static async Task RunAsync(ModesDemoWorker worker)
    {
        Console.WriteLine();
        Console.WriteLine("===== EXCLUSIVE PARALLEL OVERLAP =====");
        worker.ClearIntervals();
        await Task.WhenAll(
            worker.OverlapA1Async(),
            worker.OverlapA2Async(),
            worker.OverlapA3Async());
        worker.AssertParallelOverlapped("overlap-a");
        Console.WriteLine("ASSERT OK: overlap-a methods overlapped (ExclusiveParallel).");
        Console.WriteLine("------------------");
    }
}
