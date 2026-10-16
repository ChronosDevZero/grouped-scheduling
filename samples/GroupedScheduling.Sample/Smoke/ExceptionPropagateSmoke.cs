namespace GroupedScheduling.Sample.Smoke;

internal static class ExceptionPropagateSmoke
{
    public static async Task RunAsync(LifecycleDemoWorker worker)
    {
        Console.WriteLine();
        Console.WriteLine("===== EXCEPTION PROPAGATION + LANE FREE =====");

        var boom = worker.BoomAsync();
        var next = worker.AfterBoomAsync();

        try
        {
            await boom;
            throw new InvalidOperationException("Expected BoomAsync to throw.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "sample-boom")
        {
            Console.WriteLine($"  BoomAsync failed as expected: {ex.Message}");
        }

        await next;
        if (!worker.AfterBoomRan)
            throw new InvalidOperationException("AfterBoomAsync should run after boom frees the exclusive lane.");

        Console.WriteLine("ASSERT OK: exception reached caller and sibling group ran.");
        Console.WriteLine("------------------");
    }
}
