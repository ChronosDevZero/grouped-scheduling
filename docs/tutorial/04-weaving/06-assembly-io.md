# File: `SchedulingModuleWeaver.AssemblyIO.cs`

**Path:** `GroupedScheduling.Weaver/SchedulingModuleWeaver.AssemblyIO.cs`

## Why this file exists

`Weave(ModuleDefinition)` is host-agnostic. Real builds need: open DLL (+ PDB), resolve references, weave, write back. That is `WeaveAssembly`.

## Full flow

```csharp
public static WeaveResult WeaveAssembly(string assemblyPath, string? outputPath = null, WeaveOptions? options = null)
{
    options ??= new WeaveOptions();
    var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(dir);
    foreach (var searchDir in options.AssemblySearchDirectories)
        resolver.AddSearchDirectory(searchDir);

    var readSymbols = File.Exists(pdbPath);
    var readerParams = new ReaderParameters
    {
        ReadWrite = outputPath is null,   // in-place
        InMemory = outputPath is not null,
        AssemblyResolver = resolver,
        ReadSymbols = readSymbols,
    };

    using var assembly = AssemblyDefinition.ReadAssembly(fullPath, readerParams);
    var result = Weave(assembly.MainModule, options);
    if (!result.Success)
        return result;   // do not write broken IL

    // Write in-place or to outputPath; WriteSymbols if PDB was read
}
```

## Line walkthrough highlights

| Choice | Why |
|---|---|
| Search dir = assembly folder + ReferencePaths | Cecil can `Resolve()` GroupedScheduling / bases |
| `ReadWrite` when in-place | MSBuild weaves the intermediate DLL |
| `InMemory` when `outputPath` set | Tests can write elsewhere |
| Skip write on `!Success` | Never ship half-woven assemblies |
| `WriteSymbols` | Keep debugging / sequence points aligned |

## MSBuild vs tests

- **MSBuild task:** `WeaveAssembly(AssemblyPath, options)` — in-place on `obj/.../YourProject.dll`.
- **Unit tests:** often weave fixture assemblies with explicit options / paths.

## What breaks if…

| Change | Failure |
|---|---|
| No reference search dirs | Resolve failures → GSWEAVE000 |
| Write even when errors | Corrupt output DLL |
| Ignore PDB | Worse debugging after weave |
