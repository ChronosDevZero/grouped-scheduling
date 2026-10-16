namespace GroupedScheduling.Sample.Smoke;

internal static class ValueTaskSmoke
{
    public static async Task RunAsync(ValueTaskDemoWorker worker)
    {
        Console.WriteLine();
        Console.WriteLine("===== VALUETASK + TUPLE RESULT =====");

        await worker.PingAsync("vt-ping");
        if (worker.LastPing != "vt-ping")
            throw new InvalidOperationException($"Expected LastPing=vt-ping, got {worker.LastPing}");

        var score = await worker.ScoreAsync(21);
        if (score != 42 || worker.LastScore != 42)
            throw new InvalidOperationException($"Expected score=42, got {score} / LastScore={worker.LastScore}");

        var pair = await worker.PairAsync("widgets", 3);
        if (pair is not ("widgets", 3))
            throw new InvalidOperationException($"Expected (widgets, 3), got {pair}");

        Console.WriteLine("ASSERT OK: ValueTask, ValueTask<T>, and Task<(T1,T2)>.");
        Console.WriteLine("------------------");
    }
}
