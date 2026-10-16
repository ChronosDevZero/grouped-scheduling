namespace GroupedScheduling.Sample.Smoke;

internal static class StaticOnlySmoke
{
    public static async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("===== STATIC-ONLY WORKER =====");

        StaticOnlyJobs.Hits = 0;
        StaticOnlyJobs.LastLabel = null;

        await StaticOnlyJobs.BootAsync();
        await StaticOnlyJobs.LabelAsync("boot-label");
        var sum = await StaticOnlyJobs.SumAsync([10, 20, 12]);

        if (StaticOnlyJobs.Hits < 2)
            throw new InvalidOperationException($"Expected Hits≥2, got {StaticOnlyJobs.Hits}");
        if (StaticOnlyJobs.LastLabel != "boot-label")
            throw new InvalidOperationException($"Expected LastLabel=boot-label, got {StaticOnlyJobs.LastLabel}");
        if (sum != 42)
            throw new InvalidOperationException($"Expected sum=42, got {sum}");

        Console.WriteLine("ASSERT OK: static-only Boot/Label/Sum.");
        Console.WriteLine("------------------");
    }
}