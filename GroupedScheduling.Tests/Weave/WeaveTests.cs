using System.Reflection;
using System.Runtime.Loader;
using GroupedScheduling.Weaver;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace GroupedScheduling.Tests.Weave;

public class WeaveTests
{
    [Fact]
    public void WeaveAssembly_Succeeds_OnFixture()
    {
        var fixturePath = LocateFixtureAssembly();
        var output = Path.Combine(Path.GetTempPath(), "GroupedScheduling.Fixture.Woven." + Guid.NewGuid().ToString("N") + ".dll");

        var result = SchedulingModuleWeaver.WeaveAssembly(fixturePath, output);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        // Fixture is MSBuild-woven already; re-weave is idempotent (0 new methods) but must succeed.
        Assert.True(File.Exists(output));
        Assert.True(result.MethodsWoven >= 0);
    }

    [Fact]
    public async Task Woven_BusinessArgs_ReachOriginal()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var instance = Activator.CreateInstance(type)!;

            var save = type.GetMethod("SaveAsync")!;
            await (Task)save.Invoke(instance, ["order-42", 99, CancellationToken.None])!;

            var format = type.GetMethod("FormatAsync")!;
            var formatted = await (Task<string>)format.Invoke(instance, ["pfx", 3])!;
            Assert.Equal("pfx-3", formatted);

            var log = (List<string>)type.GetProperty("Log")!.GetValue(instance)!;
            Assert.Contains("save:order-42:99", log);

            var staticType = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleStatic")!;
            var bootLabel = staticType.GetMethod("BootWithLabelAsync")!;
            await (Task)bootLabel.Invoke(null, ["hello"])!;
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_NestedType_IsWoven()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService+NestedWorker")!;
            Assert.NotNull(type);
            var instance = Activator.CreateInstance(type)!;
            var work = type.GetMethod("NestedWorkAsync")!;
            await (Task)work.Invoke(instance, ["tag-a"])!;

            var log = (List<string>)type.GetProperty("Log")!.GetValue(instance)!;
            Assert.Contains("nested:tag-a", log);

            Assert.NotNull(type.GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic));
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Woven_Dispose_CallsBaseDispose()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var instance = Activator.CreateInstance(type)!;
            type.GetMethod("Dispose")!.Invoke(instance, null);

            var baseDisposed = (bool)type.GetProperty("BaseDisposed")!.GetValue(instance)!;
            Assert.True(baseDisposed);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_WorkAsync_RunsThroughScheduler_AndDisposeShutsDown()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var instance = Activator.CreateInstance(type)!;
            var work = type.GetMethod("WorkAsync")!;
            var task = (Task)work.Invoke(instance, null)!;
            await task;

            var log = (List<string>)type.GetProperty("Log")!.GetValue(instance)!;
            Assert.Contains("work", log);

            var dispose = type.GetMethod("Dispose")!;
            dispose.Invoke(instance, null);

            var stateField = type.GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var state = stateField.GetValue(instance)!;
            var handler = state.GetType().GetProperty("Handler")!.GetValue(state)!;
            var shuttingDown = (bool)handler.GetType().GetProperty("IsShuttingDown")!.GetValue(handler)!;
            Assert.True(shuttingDown);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_NestedSameInstance_Throws()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var instance = Activator.CreateInstance(type)!;
            var nested = type.GetMethod("NestedAsync")!;
            var task = (Task)nested.Invoke(instance, null)!;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
            Assert.Contains("Nested scheduled call", ex.Message);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_TaskOfT_And_Static_Work()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var instance = Activator.CreateInstance(type)!;
            var getValue = type.GetMethod("GetValueAsync")!;
            var taskObj = getValue.Invoke(instance, null)!;
            var resultTask = (Task<int>)taskObj;
            Assert.Equal(7, await resultTask);

            var staticType = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleStatic")!;
            var boot = staticType.GetMethod("BootAsync")!;
            await (Task)boot.Invoke(null, null)!;
            var hits = (int)staticType.GetField("Hits")!.GetValue(null)!;
            Assert.True(hits >= 1);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_GenericMethod_InstanceAndStatic_Work()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.GenericMethodHost")!;
            var instance = Activator.CreateInstance(type)!;

            var accept = type.GetMethod("AcceptAsync")!.MakeGenericMethod(typeof(int));
            await (Task)accept.Invoke(instance, [7, "n", CancellationToken.None])!;
            var log = (List<string>)type.GetProperty("Log")!.GetValue(instance)!;
            Assert.Contains("accept:n:7", log);

            var identity = type.GetMethod("IdentityAsync")!.MakeGenericMethod(typeof(string));
            var echoed = await (Task<string>)identity.Invoke(instance, ["hello"])!;
            Assert.Equal("hello", echoed);

            var count = type.GetMethod("CountAsync")!.MakeGenericMethod(typeof(int));
            var n = await (Task<int>)count.Invoke(null, [new[] { 1, 2, 3 }])!;
            Assert.Equal(3, n);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_GenericDeclaringType_InstanceAndStatic_Work()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var open = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.GenericStore`1")!;
            var closed = open.MakeGenericType(typeof(int));
            var instance = Activator.CreateInstance(closed)!;

            var save = closed.GetMethod("SaveAsync")!;
            await (Task)save.Invoke(instance, [42, "n", CancellationToken.None])!;
            var log = (List<string>)closed.GetProperty("Log")!.GetValue(instance)!;
            Assert.Contains("save:n:42", log);

            var echo = closed.GetMethod("EchoAsync")!;
            var echoed = await (Task<int>)echo.Invoke(instance, [99])!;
            Assert.Equal(99, echoed);

            var before = (int)closed.GetField("Hits")!.GetValue(null)!;
            await (Task)closed.GetMethod("BootAsync")!.Invoke(null, null)!;
            var after = (int)closed.GetField("Hits")!.GetValue(null)!;
            Assert.True(after > before);

            // Separate closed type has its own static state/hits counter field (shared static field storage per closed type).
            var closedString = open.MakeGenericType(typeof(string));
            var hitsStrBefore = (int)closedString.GetField("Hits")!.GetValue(null)!;
            await (Task)closedString.GetMethod("BootAsync")!.Invoke(null, null)!;
            Assert.Equal(hitsStrBefore + 1, (int)closedString.GetField("Hits")!.GetValue(null)!);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_ValueTask_And_ValueTaskOfT_Work()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var instance = Activator.CreateInstance(type)!;

            var ping = type.GetMethod("PingValueTaskAsync")!;
            await (ValueTask)ping.Invoke(instance, null)!;

            var getVt = type.GetMethod("GetValueTaskAsync")!;
            Assert.Equal(11, await (ValueTask<int>)getVt.Invoke(instance, null)!);

            var save = type.GetMethod("SaveValueTaskAsync")!;
            await (ValueTask)save.Invoke(instance, ["label-vt", CancellationToken.None])!;
            var log = (List<string>)type.GetProperty("Log")!.GetValue(instance)!;
            Assert.Contains("vt-save:label-vt", log);

            var staticType = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleStatic")!;
            var before = (int)staticType.GetField("Hits")!.GetValue(null)!;
            await (ValueTask)staticType.GetMethod("BootValueTaskAsync")!.Invoke(null, null)!;
            var after = (int)staticType.GetField("Hits")!.GetValue(null)!;
            Assert.True(after > before);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_Cancelable_RespectsObjectShutdownToken()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var instance = Activator.CreateInstance(type)!;
            var method = type.GetMethod("CancelableAsync")!;
            var task = (Task)method.Invoke(instance, [CancellationToken.None])!;

            await Task.Delay(50);
            type.GetMethod("Dispose")!.Invoke(instance, null);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_MethodGroup_And_Reflection_Schedule()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var instance = Activator.CreateInstance(type)!;
            var work = type.GetMethod("WorkAsync")!;

            // Method group / delegate
            var del = (Func<Task>)Delegate.CreateDelegate(typeof(Func<Task>), instance, work);
            await del();

            // Reflection invoke (already routes through woven stub)
            await (Task)work.Invoke(instance, null)!;

            var log = (List<string>)type.GetProperty("Log")!.GetValue(instance)!;
            Assert.True(log.Count(x => x == "work") >= 2);

            Assert.Contains(
                work.GetCustomAttributes(inherit: false),
                a => a.GetType().Name == "DebuggerStepThroughAttribute");
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public async Task Woven_CrossInstance_Nest_IsAllowed()
    {
        var alc = LoadWovenFixture(out var asm);
        try
        {
            var type = asm.GetType("GroupedScheduling.Fixture.WeaveSubjects.SampleService")!;
            var a = Activator.CreateInstance(type)!;
            var b = Activator.CreateInstance(type)!;
            var work = type.GetMethod("WorkAsync")!;

            await (Task)work.Invoke(a, null)!;
            await (Task)work.Invoke(b, null)!;

            // A schedules work that awaits B's woven method (different handler) — must not throw.
            var ran = false;
            var stateField = type.GetField("__groupedSchedulingState", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var stateA = stateField.GetValue(a)!;
            var handlerA = stateA.GetType().GetProperty("Handler")!.GetValue(stateA)!;
            var schedule = handlerA.GetType().GetMethod(
                "ScheduleTask",
                [typeof(Func<Task>), typeof(int), typeof(CancellationToken)])!;

            var task = (Task)schedule.Invoke(handlerA, [(Func<Task>)(async () =>
            {
                await (Task)work.Invoke(b, null)!;
                ran = true;
            }), 1, CancellationToken.None])!;
            await task;
            Assert.True(ran);
        }
        finally
        {
            alc.Unload();
        }
    }

    [Fact]
    public void Weave_InheritanceConflict_FailsWithGSWEAVE002()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gs-inh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var runtime = typeof(ScheduleGroupAttribute).Assembly.Location;
            var code = """
                using System.Threading.Tasks;
                using GroupedScheduling;
                public class BaseSvc {
                  [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                  public async Task BaseAsync() { await Task.Yield(); }
                }
                public class DerivedSvc : BaseSvc {
                  [ScheduleGroup("b", ScheduleMode.ExclusiveSerial)]
                  public async Task DerivedAsync() { await Task.Yield(); }
                }
                """;

            var syntax = CSharpSyntaxTree.ParseText(code);
            var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
            var refs = tpa
                .Select(p => MetadataReference.CreateFromFile(p))
                .Append(MetadataReference.CreateFromFile(runtime))
                .ToList();

            var compilation = CSharpCompilation.Create(
                "ConflictFixture",
                [syntax],
                refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var dll = Path.Combine(dir, "ConflictFixture.dll");
            var emit = compilation.Emit(dll);
            Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

            var result = SchedulingModuleWeaver.WeaveAssembly(dll, Path.Combine(dir, "ConflictFixture.Woven.dll"));
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Id == "GSWEAVE002");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    private static AssemblyLoadContext LoadWovenFixture(out Assembly asm)
    {
        var fixturePath = LocateFixtureAssembly();
        var output = Path.Combine(Path.GetTempPath(), "GroupedScheduling.Fixture.Woven." + Guid.NewGuid().ToString("N") + ".dll");
        var result = SchedulingModuleWeaver.WeaveAssembly(fixturePath, output);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        var alc = new AssemblyLoadContext("woven-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        alc.Resolving += (_, name) =>
        {
            var candidate = Path.Combine(Path.GetDirectoryName(fixturePath)!, name.Name + ".dll");
            return File.Exists(candidate) ? alc.LoadFromAssemblyPath(candidate) : null;
        };
        asm = alc.LoadFromAssemblyPath(output);
        return alc;
    }

    private static string LocateFixtureAssembly()
    {
        var dir = AppContext.BaseDirectory;
        var path = Path.Combine(dir, "GroupedScheduling.Fixture.dll");
        if (File.Exists(path))
            return path;

        throw new FileNotFoundException("GroupedScheduling.Fixture.dll not found next to tests.", path);
    }
}
