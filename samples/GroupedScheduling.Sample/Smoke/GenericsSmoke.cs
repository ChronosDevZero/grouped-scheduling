namespace GroupedScheduling.Sample.Smoke;

internal static class GenericsSmoke
{
    public static async Task RunAsync(EntityStore<int> ints, EntityStore<string> strings, GenericMethodHost methods)
    {
        Console.WriteLine();
        Console.WriteLine("===== GENERIC DECLARING TYPE (EntityStore<T>) =====");

        await EntityStore<int>.BootAsync();
        await EntityStore<string>.BootAsync();
        if (EntityStore<int>.Hits < 1 || EntityStore<string>.Hits < 1)
            throw new InvalidOperationException("Static BootAsync did not bump Hits per closed type.");

        await ints.SaveAsync(42, "answer");
        var echoedInt = await ints.EchoAsync(99);
        if (echoedInt != 99 || !ints.Log.Contains("save:answer:42"))
            throw new InvalidOperationException("EntityStore<int> save/echo failed.");

        await strings.SaveAsync("hello", "greet");
        var echoedStr = await strings.EchoAsync("world");
        if (echoedStr != "world" || !strings.Log.Contains("save:greet:hello"))
            throw new InvalidOperationException("EntityStore<string> save/echo failed.");

        Console.WriteLine("ASSERT OK: EntityStore<int> and EntityStore<string> are independent.");
        Console.WriteLine("------------------");

        Console.WriteLine();
        Console.WriteLine("===== GENERIC METHODS (AcceptAsync<T> / IdentityAsync<T>) =====");

        await methods.AcceptAsync(7, "n");
        await methods.AcceptAsync("x", "s");
        if (!methods.Log.Contains("accept:n:7") || !methods.Log.Contains("accept:s:x"))
            throw new InvalidOperationException("AcceptAsync<T> did not log both specializations.");

        var id = await methods.IdentityAsync("hello");
        if (id != "hello")
            throw new InvalidOperationException($"IdentityAsync expected hello, got {id}");

        var count = await GenericMethodHost.CountAsync(new[] { 1, 2, 3 });
        if (count != 3)
            throw new InvalidOperationException($"CountAsync expected 3, got {count}");

        Console.WriteLine("ASSERT OK: generic methods (instance + static).");
        Console.WriteLine("------------------");
    }
}
