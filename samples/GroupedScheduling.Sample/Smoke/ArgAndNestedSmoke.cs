namespace GroupedScheduling.Sample.Smoke;

internal static class ArgAndNestedSmoke
{
    public static async Task RunAsync(ModesDemoWorker worker, AuxWorker aux)
    {
        Console.WriteLine("===== ARG + NESTED TYPE CHECK =====");
        await worker.EchoAsync("hello-from-weave", times: 2);
        var nested = new ModesDemoWorker.NestedJobs();
        await nested.RunAsync("job-7");
        await aux.PingAsync();
        if (!aux.LastPing)
            throw new InvalidOperationException("AuxWorker.PingAsync did not run.");
        Console.WriteLine("------------------");
    }
}
