namespace GroupedScheduling.Weaver;

public sealed class WeaveOptions
{
    /// <summary>
    /// Extra directories for Mono.Cecil to resolve referenced assemblies (e.g. MSBuild ReferencePath folders).
    /// </summary>
    public List<string> AssemblySearchDirectories { get; } = new();
}
