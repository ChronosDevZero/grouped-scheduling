namespace GroupedScheduling.Sample.Smoke;

internal static class CrossInstanceNestSmoke
{
    public static async Task RunAsync(LifecycleDemoWorker a, LifecycleDemoWorker b)
    {
        Console.WriteLine();
        Console.WriteLine("===== CROSS-INSTANCE NEST (A→B ok) =====");
        a.Peer = b;
        await a.CallPeerAsync();
        if (!b.PeerWorkRan)
            throw new InvalidOperationException("Peer work did not run.");
        Console.WriteLine("ASSERT OK: A awaiting B on another instance is allowed.");
        Console.WriteLine("------------------");
    }
}
