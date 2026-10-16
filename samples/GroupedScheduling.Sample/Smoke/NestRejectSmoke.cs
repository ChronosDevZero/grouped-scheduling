using System.Reflection;
using GroupedScheduling;

namespace GroupedScheduling.Sample.Smoke;

internal static class NestRejectSmoke
{
    public static async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("===== NEST REJECT (same-instance, runtime) =====");
        Console.WriteLine("  (TSCH001 catches the source call; this path uses reflection to hit the runtime guard.)");

        var worker = new NestDemoWorker();
        try
        {
            await worker.OuterViaReflectionAsync();
            throw new InvalidOperationException("Expected nested same-instance call to throw.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Nested scheduled call", StringComparison.Ordinal))
        {
            Console.WriteLine($"  nested call failed as expected: {ex.Message}");
        }

        Console.WriteLine("ASSERT OK: same-instance nest is rejected at runtime.");
        Console.WriteLine("------------------");
    }

    private sealed class NestDemoWorker
    {
        [ScheduleGroup("nest", ScheduleMode.ExclusiveSerial)]
        public async Task OuterViaReflectionAsync(CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            Console.WriteLine($"[{start:HH:mm:ss.fff}] START Nest.OuterViaReflectionAsync");
            // Bypass TSCH001 so we still prove the ActionsHandler nest guard.
            var inner = GetType().GetMethod(nameof(InnerAsync), BindingFlags.Instance | BindingFlags.Public)!;
            await (Task)inner.Invoke(this, [cancellationToken])!;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] DONE  Nest.OuterViaReflectionAsync (should not reach)");
        }

        [ScheduleGroup("nest", ScheduleMode.ExclusiveSerial)]
        public async Task InnerAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] START Nest.InnerAsync (should not run under Outer)");
            await Task.Yield();
        }
    }
}
