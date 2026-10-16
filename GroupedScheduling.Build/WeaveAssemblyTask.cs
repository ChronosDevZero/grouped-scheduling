using GroupedScheduling.Weaver;
using Microsoft.Build.Framework;
using MsBuildTask = Microsoft.Build.Utilities.Task;

namespace GroupedScheduling.Build;

/// <summary>
/// MSBuild task that weaves <see cref="ScheduleGroupAttribute"/> methods in-place after compile.
/// </summary>
public sealed class WeaveAssembly : MsBuildTask
{
    [Required]
    public string AssemblyPath { get; set; } = "";

    /// <summary>Compile references — used so Cecil can resolve GroupedScheduling and others.</summary>
    public ITaskItem[]? ReferencePaths { get; set; }

    public override bool Execute()
    {
        if (string.IsNullOrWhiteSpace(AssemblyPath) || !File.Exists(AssemblyPath))
        {
            Log.LogError("GSBUILD001", null, null, null, 0, 0, 0, 0,
                "GroupedScheduling weave skipped: assembly not found at '{0}'.", AssemblyPath);
            return false;
        }

        try
        {
            var options = new WeaveOptions();
            if (ReferencePaths is not null)
            {
                foreach (var item in ReferencePaths)
                {
                    var path = item.ItemSpec;
                    if (string.IsNullOrWhiteSpace(path))
                        continue;
                    var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                    if (!string.IsNullOrWhiteSpace(dir))
                        options.AssemblySearchDirectories.Add(dir);
                }
            }

            var result = SchedulingModuleWeaver.WeaveAssembly(AssemblyPath, options: options);
            foreach (var d in result.Diagnostics)
            {
                switch (d.Severity)
                {
                    case WeaveSeverity.Error:
                        Log.LogError(null, d.Id, null, null, 0, 0, 0, 0, d.Message);
                        break;
                    case WeaveSeverity.Warning:
                        Log.LogWarning(null, d.Id, null, null, 0, 0, 0, 0, d.Message);
                        break;
                    default:
                        Log.LogMessage(MessageImportance.Low, "{0}: {1}", d.Id, d.Message);
                        break;
                }
            }

            if (result.Success)
            {
                Log.LogMessage(
                    MessageImportance.Normal,
                    "GroupedScheduling: woven {0} method(s) in {1}",
                    result.MethodsWoven,
                    AssemblyPath);
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            Log.LogError("GSBUILD002", null, null, null, 0, 0, 0, 0,
                "GroupedScheduling weave failed: {0}", ex.Message);
            return false;
        }
    }
}
