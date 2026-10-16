using GroupedScheduling.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace GroupedScheduling.Tests.Analyzers;

public class AnalyzerTests
{
    private static string Source(string body) => """
        using System.Threading.Tasks;
        using GroupedScheduling;

        namespace GroupedScheduling
        {
            public enum ScheduleMode { FullyConcurrent = 0, ExclusiveSerial = 1, ExclusiveParallel = 2 }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class ScheduleGroupAttribute : System.Attribute
            {
                public ScheduleGroupAttribute(string name, ScheduleMode mode) { }
            }
        }

        """ + body;

    [Fact]
    public async Task TSCH001_NestedSameInstance_Reports()
    {
        var test = new CSharpAnalyzerTest<NestedSameInstanceAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Outer()
                    {
                        await {|TSCH001:Inner()|};
                    }

                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Inner() { await Task.Yield(); }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH001_DifferentInstance_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<NestedSameInstanceAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Outer(C other)
                    {
                        await other.Inner();
                    }

                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Inner() { await Task.Yield(); }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH002_MissingWeaver_Reports()
    {
        var test = new CSharpAnalyzerTest<MissingWeaverAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task {|TSCH002:Work|}() { await Task.Yield(); }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH002_WeaverImported_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<MissingWeaverAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Work() { await Task.Yield(); }
                }
                """),
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", """
            is_global = true
            build_property.GroupedSchedulingWeaverImported = true
            """));
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH003_InheritanceConflict_Reports()
    {
        var test = new CSharpAnalyzerTest<InheritanceConflictAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class Base
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task BaseWork() { await Task.Yield(); }
                }

                class {|TSCH003:Derived|} : Base
                {
                    [ScheduleGroup("b", ScheduleMode.ExclusiveSerial)]
                    public async Task DerivedWork() { await Task.Yield(); }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_BadReturnType_Reports()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public void {|TSCH004:Bad|}() { }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_SyncIntReturn_Reports()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public int {|TSCH004:Bad|}() => 1;
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_ValueTask_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async ValueTask Ping() { await Task.Yield(); }

                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async ValueTask<int> Get(System.Threading.CancellationToken cancellationToken)
                    {
                        await Task.Yield();
                        return 1;
                    }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_GenericMethod_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Work<T>(T item, System.Threading.CancellationToken cancellationToken)
                    {
                        await Task.Yield();
                    }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_GenericDeclaringType_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class Repo<T>
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Save(T item, System.Threading.CancellationToken cancellationToken)
                    {
                        await Task.Yield();
                    }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_ByRefParameter_Reports()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public Task {|TSCH004:Bad|}(ref int x) => Task.CompletedTask;
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_BusinessArgs_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Save(string label, int value, System.Threading.CancellationToken cancellationToken)
                    {
                        await Task.Yield();
                    }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH005_MissingCancellationToken_Reports()
    {
        var test = new CSharpAnalyzerTest<MissingCancellationTokenAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task {|TSCH005:Work|}() { await Task.Yield(); }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH005_WithCancellationToken_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<MissingCancellationTokenAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Work(System.Threading.CancellationToken cancellationToken) { await Task.Yield(); }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH001_CrossInstance_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<NestedSameInstanceAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Outer(C other)
                    {
                        await other.Inner();
                    }

                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public async Task Inner() { await Task.Yield(); }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_NonTaskReturn_Reports()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                    public void {|TSCH004:Bad|}() { }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH004_ExclusiveParallel_WithArgs_NoDiagnostic()
    {
        var test = new CSharpAnalyzerTest<UnsupportedSignatureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class C
                {
                    [ScheduleGroup("a", ScheduleMode.ExclusiveParallel)]
                    public async Task Work(string id, System.Threading.CancellationToken cancellationToken)
                    {
                        await Task.Yield();
                    }
                }
                """),
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task TSCH005_NestedType_Reports()
    {
        var test = new CSharpAnalyzerTest<MissingCancellationTokenAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source("""
                class Outer
                {
                    class Inner
                    {
                        [ScheduleGroup("a", ScheduleMode.ExclusiveSerial)]
                        public async Task {|TSCH005:Work|}() { await Task.Yield(); }
                    }
                }
                """),
        };
        await test.RunAsync();
    }
}
