using System.Reflection;
using GroupedScheduling;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace GroupedScheduling.Weaver;
public static partial class SchedulingModuleWeaver
{
    public static WeaveResult WeaveAssembly(string assemblyPath, string? outputPath = null, WeaveOptions? options = null)
    {
        options ??= new WeaveOptions();
        var fullPath = Path.GetFullPath(assemblyPath);
        var dir = Path.GetDirectoryName(fullPath)!;
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(dir);
        foreach (var searchDir in options.AssemblySearchDirectories)
        {
            if (!string.IsNullOrWhiteSpace(searchDir) && Directory.Exists(searchDir))
                resolver.AddSearchDirectory(searchDir);
        }

        var pdbPath = Path.ChangeExtension(fullPath, ".pdb");
        var readSymbols = File.Exists(pdbPath);

        var readerParams = new ReaderParameters
        {
            ReadWrite = outputPath is null,
            InMemory = outputPath is not null,
            AssemblyResolver = resolver,
            ReadSymbols = readSymbols,
        };

        using var assembly = AssemblyDefinition.ReadAssembly(fullPath, readerParams);
        var result = Weave(assembly.MainModule, options);
        if (!result.Success)
            return result;

        var writerParams = new WriterParameters { WriteSymbols = readSymbols };
        if (outputPath is null)
            assembly.Write(writerParams);
        else
            assembly.Write(outputPath, writerParams);

        return result;
    }
}

