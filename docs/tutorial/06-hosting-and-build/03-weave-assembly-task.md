# File: `WeaveAssemblyTask.cs` + MSBuild targets

**Paths:**

- `GroupedScheduling.Build/WeaveAssemblyTask.cs` (class `WeaveAssembly`)
- `GroupedScheduling.Build/build/GroupedScheduling.Build.targets`

## Why this exists

The weaver is a library. Builds need an MSBuild **task** that runs after `CoreCompile` and fails the build on `GSWEAVE*` errors.

## Task `Execute`

```csharp
public sealed class WeaveAssembly : Microsoft.Build.Utilities.Task
{
    [Required] public string AssemblyPath { get; set; }
    public ITaskItem[]? ReferencePaths { get; set; }

    public override bool Execute()
    {
        // 1) assembly missing -> GSBUILD001, return false
        // 2) fill WeaveOptions.AssemblySearchDirectories from ReferencePaths
        // 3) SchedulingModuleWeaver.WeaveAssembly(AssemblyPath, options)
        // 4) map diagnostics to LogError / LogWarning / LogMessage
        // 5) log "woven N method(s)" on success
        // 6) catch -> GSBUILD002
        // return result.Success
    }
}
```

**Concept:** returning `false` from an MSBuild task fails the build. That is how weave errors become red builds.

## Targets file (the hook)

```xml
<UsingTask TaskName="GroupedScheduling.Build.WeaveAssembly"
           AssemblyFile="$(GroupedSchedulingTaskAssembly)"
           Condition="'$(GroupedSchedulingWeaveEnabled)' == 'true'" />

<Target Name="GroupedSchedulingWeave"
        AfterTargets="CoreCompile"
        BeforeTargets="CopyFilesToOutputDirectory;CopyFilesToPublishDirectory"
        Condition="... WeaveEnabled, not DesignTimeBuild, task exists ..."
        Inputs="...(intermediate dll); task dll; weaver dll"
        Outputs="...(intermediate dll).groupedscheduling.stamp">
  <GroupedScheduling.Build.WeaveAssembly
      AssemblyPath="...(intermediate dll)"
      ReferencePaths="@(ReferencePath);@(ReferencePathWithRefAssemblies)" />
  <Touch ... stamp AlwaysCreate="true" />
</Target>
```

| Detail | Why |
|---|---|
| After `CoreCompile` | DLL exists to rewrite |
| Before copy to output | Output folder gets woven bits |
| Skip `DesignTimeBuild` | IDE design-time builds should not mutate |
| Stamp Outputs | Incremental: skip weave when inputs unchanged |
| `GroupedSchedulingWeaveEnabled` | Escape hatch (`false` disables) |

## Disable weaving

```xml
<PropertyGroup>
  <GroupedSchedulingWeaveEnabled>false</GroupedSchedulingWeaveEnabled>
</PropertyGroup>
```

Without weaving, `[ScheduleGroup]` is inert (TSCH002 may still fire if targets are not imported at all).

## See also

- `docs/packaging.md`, `docs/weaving.md`
- Tests: `MsBuildWeaveTests.cs`
