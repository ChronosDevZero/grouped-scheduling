using System.Reflection;
using System.Runtime.Loader;
using GroupedScheduling.Weaver;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace GroupedScheduling.Tests.Weave;

public class WeaveCoverageTests
{
    [Fact]
    public async Task ArgMatrix_AllShapes_ReachOriginal()
    {
        var alc = LoadWoven(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.ArgMatrixService")!;
            var instance = Activator.CreateInstance(type)!;
            var log = (List<string>)type.GetProperty("Log")!.GetValue(instance)!;

            await (Task)type.GetMethod("ZeroAsync")!.Invoke(instance, null)!;
            await (Task)type.GetMethod("OnlyTokenAsync")!.Invoke(instance, [CancellationToken.None])!;
            await (Task)type.GetMethod("ManyValueAsync")!.Invoke(instance, [1, 2, 3, 4, 5, 6, 7, 8])!;
            await (Task)type.GetMethod("ManyRefAsync")!.Invoke(instance, ["a", "b", "c", 42])!;
            var typed = await (Task<string>)type.GetMethod("TypedWithArgsAsync")!
                .Invoke(instance, ["p", 9, CancellationToken.None])!;
            Assert.Equal("p:9", typed);
            await (Task)type.GetMethod("TokenNotLastAsync")!.Invoke(instance, ["L", CancellationToken.None, 3])!;
            await (Task)type.GetMethod("ConcurrentWithArgsAsync")!.Invoke(instance, ["go"])!;

            Assert.Contains("zero", log);
            Assert.Contains("only-token", log);
            Assert.Contains("vals:12345678", log);
            Assert.Contains("refs:a:b:c:42", log);
            Assert.Contains("mid-ct:L:3", log);
            Assert.Contains("conc:go", log);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task DeeperNestedType_IsWoven()
    {
        var alc = LoadWoven(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.OuterNest+InnerNest+DeeperNest")!;
            var instance = Activator.CreateInstance(type)!;
            await (Task)type.GetMethod("DeepAsync")!.Invoke(instance, ["x"])!;
            var log = (List<string>)type.GetProperty("Log")!.GetValue(instance)!;
            Assert.Contains("deep:x", log);
            Assert.NotNull(type.GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic));
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task MultiCtor_AllInitState()
    {
        var alc = LoadWoven(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.MultiCtorService")!;
            foreach (var args in new object[][] { [], ["m1"], ["m2", 0] })
            {
                var instance = Activator.CreateInstance(type, args)!;
                Assert.NotNull(type.GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance));
                await (Task)type.GetMethod("WorkAsync")!.Invoke(instance, ["t"])!;
            }
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void OwnDispose_IsWrapped_AndStillRuns()
    {
        var alc = LoadWoven(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.OwnDisposeService")!;
            var instance = Activator.CreateInstance(type)!;
            type.GetMethod("Dispose")!.Invoke(instance, null);
            Assert.True((bool)type.GetProperty("OwnDisposeCalled")!.GetValue(instance)!);

            var state = type.GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
            var handler = state.GetType().GetProperty("Handler")!.GetValue(state)!;
            Assert.True((bool)handler.GetType().GetProperty("IsShuttingDown")!.GetValue(handler)!);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Idempotent_SecondWeave_DoesNotDoubleWrap()
    {
        var fixturePath = LocateFixture();
        var once = Path.Combine(Path.GetTempPath(), "gs-once-" + Guid.NewGuid().ToString("N") + ".dll");
        var twice = Path.Combine(Path.GetTempPath(), "gs-twice-" + Guid.NewGuid().ToString("N") + ".dll");

        // Fixture DLL is already MSBuild-woven; both passes should succeed and weave nothing new.
        var r1 = SchedulingModuleWeaver.WeaveAssembly(fixturePath, once);
        Assert.True(r1.Success, string.Join(Environment.NewLine, r1.Diagnostics));
        Assert.Equal(0, r1.MethodsWoven);

        var r2 = SchedulingModuleWeaver.WeaveAssembly(once, twice);
        Assert.True(r2.Success, string.Join(Environment.NewLine, r2.Diagnostics));
        Assert.Equal(0, r2.MethodsWoven);
    }

    [Fact]
    public void ConflictingGroupModes_FailsWeave()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gs-mode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var runtime = typeof(ScheduleGroupAttribute).Assembly.Location;
            var code = """
                using System.Threading.Tasks;
                using GroupedScheduling;
                public class ConflictModes {
                  [ScheduleGroup("g", ScheduleMode.ExclusiveSerial)]
                  public async Task A() { await Task.Yield(); }
                  [ScheduleGroup("g", ScheduleMode.ExclusiveParallel)]
                  public async Task B() { await Task.Yield(); }
                }
                """;
            var dll = Compile(dir, "ConflictModes", code, runtime);
            // BuildGroupMap throws — caught as GSWEAVE000
            var result = SchedulingModuleWeaver.WeaveAssembly(dll, Path.Combine(dir, "out.dll"));
            Assert.False(result.Success);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Unsupported_ByRef_FailsWeave()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gs-ref-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var runtime = typeof(ScheduleGroupAttribute).Assembly.Location;
            var code = """
                using System.Threading.Tasks;
                using GroupedScheduling;
                public class RefBad {
                  [ScheduleGroup("g", ScheduleMode.ExclusiveSerial)]
                  public Task Bad(ref int x) { x = 1; return Task.CompletedTask; }
                }
                """;
            var dll = Compile(dir, "RefBad", code, runtime);
            var result = SchedulingModuleWeaver.WeaveAssembly(dll, Path.Combine(dir, "out.dll"));
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Id == "GSWEAVE005");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    private static string Compile(string dir, string name, string code, string runtime)
    {
        var syntax = CSharpSyntaxTree.ParseText(code);
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var refs = tpa
            .Select(p => MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(runtime))
            .ToList();
        var compilation = CSharpCompilation.Create(
            name, [syntax], refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var dll = Path.Combine(dir, name + ".dll");
        var emit = compilation.Emit(dll);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return dll;
    }

    private static AssemblyLoadContext LoadWoven(out Assembly asm)
    {
        var fixturePath = LocateFixture();
        var output = Path.Combine(Path.GetTempPath(), "gs-cov-" + Guid.NewGuid().ToString("N") + ".dll");
        var result = SchedulingModuleWeaver.WeaveAssembly(fixturePath, output);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var alc = new AssemblyLoadContext("cov-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        alc.Resolving += (_, name) =>
        {
            var candidate = Path.Combine(Path.GetDirectoryName(fixturePath)!, name.Name + ".dll");
            return File.Exists(candidate) ? alc.LoadFromAssemblyPath(candidate) : null;
        };
        asm = alc.LoadFromAssemblyPath(output);
        return alc;
    }

    private static string LocateFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "GroupedScheduling.Fixture.dll");
        if (File.Exists(path)) return path;
        throw new FileNotFoundException("GroupedScheduling.Fixture.dll not found.", path);
    }
}
