using System.Reflection;
using System.Runtime.Loader;
using GroupedScheduling.Fixture.Workers;
using GroupedScheduling.Sample;
using Xunit;

namespace GroupedScheduling.Tests.Weave;

/// <summary>
/// Proves MSBuild-woven assemblies (Fixture + Sample) behave — not only WeaveAssembly API.
/// </summary>
public class MsBuildWeaveTests
{
    [Fact]
    public async Task Fixture_Workers_AreWoven_OnBuild()
    {
        // SerialWorker is in the Fixture project which imports GroupedScheduling.Build targets.
        var w = new SerialWorker();
        Assert.NotNull(typeof(SerialWorker).GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.NotNull(typeof(SerialWorker).GetMethod("Dispose"));

        await Task.WhenAll(w.S1Async(), w.S2Async(), w.S3Async(), w.S4Async());
        IntervalRecorder.AssertNoOverlap(w.Recorder.Snapshot(), "serial");
    }

    [Fact]
    public async Task Sample_Project_Is_Woven_On_Build()
    {
        var sampleDll = LocateSampleAssembly();
        var alc = new AssemblyLoadContext("sample-woven-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            alc.Resolving += (_, name) =>
            {
                var candidate = Path.Combine(Path.GetDirectoryName(sampleDll)!, name.Name + ".dll");
                return File.Exists(candidate) ? alc.LoadFromAssemblyPath(candidate) : null;
            };

            var asm = alc.LoadFromAssemblyPath(sampleDll);
            var type = asm.GetType("GroupedScheduling.Sample.ModesDemoWorker")!;
            Assert.NotNull(type.GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.NotNull(type.GetMethod("Dispose"));

            var instance = Activator.CreateInstance(type)!;
            var echo = type.GetMethod("EchoAsync")!;
            await (Task)echo.Invoke(instance, ["woven-ok", 2, CancellationToken.None])!;
            Assert.Equal("woven-okx2", type.GetProperty("LastEcho")!.GetValue(instance));

            var nestedType = asm.GetType("GroupedScheduling.Sample.ModesDemoWorker+NestedJobs")!;
            Assert.NotNull(nestedType.GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic));
            var nested = Activator.CreateInstance(nestedType)!;
            await (Task)nestedType.GetMethod("RunAsync")!.Invoke(nested, ["n1", CancellationToken.None])!;
            Assert.Equal("n1", nestedType.GetProperty("LastJobId")!.GetValue(nested));
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task SampleWorker_Burst_SerialNeverOverlaps()
    {
        // Direct reference to MSBuild-woven Sample assembly (project reference).
        var worker = new ModesDemoWorker();
        worker.ClearIntervals();

        await Task.WhenAll(
            worker.Serial1Async(), worker.Serial2Async(), worker.Serial3Async(), worker.Serial4Async(),
            worker.OverlapA1Async(), worker.OverlapA2Async(), worker.OverlapA3Async(),
            worker.Anytime1Async(), worker.Anytime2Async(),
            worker.OverlapB1Async(), worker.OverlapB2Async(), worker.OverlapB3Async(),
            worker.OverlapB4Async(), worker.OverlapB5Async());

        worker.AssertSerialNeverOverlapped();
        Assert.True(worker.Intervals.Count(i => i.Group == "serial") >= 4);
    }

    private static string LocateSampleAssembly()
    {
        var repoRoot = FindRepoRoot();
        var configs = new[] { "Debug", "Release" };
        foreach (var config in configs)
        {
            var path = Path.Combine(
                repoRoot,
                "samples",
                "GroupedScheduling.Sample",
                "bin",
                config,
                "net11.0",
                "GroupedScheduling.Sample.dll");
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException(
            "GroupedScheduling.Sample.dll not found. Build samples/GroupedScheduling.Sample first.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GroupedScheduling.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (GroupedScheduling.slnx).");
    }
}
