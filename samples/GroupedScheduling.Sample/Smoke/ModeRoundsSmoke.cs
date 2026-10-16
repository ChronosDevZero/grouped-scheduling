namespace GroupedScheduling.Sample.Smoke;

internal static class ModeRoundsSmoke
{
    public static async Task RunAsync(ModesDemoWorker worker)
    {
        Func<Task>[][] rounds =
        [
            [
                () => worker.Serial1Async(),
                () => worker.OverlapA1Async(),
                () => worker.Anytime1Async(),
                () => worker.OverlapB1Async(),
                () => worker.Serial2Async(),
                () => worker.OverlapA2Async(),
                () => worker.OverlapB2Async(),
                () => worker.Anytime2Async(),
                () => worker.Serial3Async(),
                () => worker.OverlapA3Async(),
                () => worker.OverlapB3Async(),
                () => worker.Serial4Async(),
                () => worker.OverlapB4Async(),
                () => worker.OverlapB5Async(),
            ],
            [
                () => worker.OverlapB5Async(),
                () => worker.OverlapB4Async(),
                () => worker.OverlapB3Async(),
                () => worker.Anytime2Async(),
                () => worker.Serial4Async(),
                () => worker.Serial3Async(),
                () => worker.OverlapA3Async(),
                () => worker.OverlapA2Async(),
                () => worker.Anytime1Async(),
                () => worker.Serial2Async(),
                () => worker.Serial1Async(),
                () => worker.OverlapB2Async(),
                () => worker.OverlapB1Async(),
                () => worker.OverlapA1Async(),
            ],
            [
                () => worker.Anytime1Async(),
                () => worker.Anytime2Async(),
                () => worker.Serial1Async(),
                () => worker.Serial2Async(),
                () => worker.Serial3Async(),
                () => worker.Serial4Async(),
                () => worker.OverlapA1Async(),
                () => worker.OverlapA2Async(),
                () => worker.OverlapA3Async(),
                () => worker.OverlapB1Async(),
                () => worker.OverlapB2Async(),
                () => worker.OverlapB3Async(),
                () => worker.OverlapB4Async(),
                () => worker.OverlapB5Async(),
            ],
            [
                () => worker.OverlapA2Async(),
                () => worker.Serial3Async(),
                () => worker.OverlapB3Async(),
                () => worker.Anytime1Async(),
                () => worker.OverlapA1Async(),
                () => worker.Serial1Async(),
                () => worker.OverlapB1Async(),
                () => worker.OverlapB5Async(),
                () => worker.Serial4Async(),
                () => worker.OverlapA3Async(),
                () => worker.Anytime2Async(),
                () => worker.OverlapB2Async(),
                () => worker.Serial2Async(),
                () => worker.OverlapB4Async(),
            ],
        ];

        worker.ClearIntervals();

        for (var i = 0; i < rounds.Length; i++)
        {
            Console.WriteLine();
            Console.WriteLine($"===== ROUND {i + 1} =====");
            await Task.WhenAll(rounds[i].Select(start => start()));
            Console.WriteLine("------------------");
        }

        Console.WriteLine();
        Console.WriteLine("===== BURST =====");
        var burst = rounds.SelectMany(r => r).Reverse().ToArray();
        await Task.WhenAll(burst.Select(start => start()));
        Console.WriteLine("------------------");

        worker.AssertSerialNeverOverlapped();
        Console.WriteLine(
            $"ASSERT OK: {worker.Intervals.Count(i => i.Group == "serial")} serial intervals never overlapped.");
    }
}
