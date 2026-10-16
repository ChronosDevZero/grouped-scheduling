namespace GroupedScheduling.Sample.Smoke;

internal static class CombinedGenericsSmoke
{
    public static async Task RunAsync(MapperDemo<int> mapper)
    {
        Console.WriteLine();
        Console.WriteLine("===== GENERIC TYPE + GENERIC METHOD =====");

        var text = await mapper.MapAsync(42, n => $"n={n}");
        if (text != "n=42")
            throw new InvalidOperationException($"Expected n=42, got {text}");

        var doubled = await mapper.MapAsync(21, n => n * 2);
        if (doubled != 42)
            throw new InvalidOperationException($"Expected 42, got {doubled}");

        if (mapper.MapCount != 2)
            throw new InvalidOperationException($"Expected MapCount=2, got {mapper.MapCount}");

        Console.WriteLine("ASSERT OK: Mapper<int>.MapAsync<string> and MapAsync<int>.");
        Console.WriteLine("------------------");
    }
}
