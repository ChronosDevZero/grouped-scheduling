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
    private static void EnsureDispose(ModuleDefinition module, TypeDefinition type, FieldDefinition instanceField)
    {
        var disposeHook = module.ImportReference(
            typeof(SchedulingWeaveHooks).GetMethod(nameof(SchedulingWeaveHooks.DisposeInstanceState))!);

        var existing = type.Methods.FirstOrDefault(m =>
            !m.IsStatic && m.Name == nameof(IDisposable.Dispose) && !m.HasParameters &&
            m.ReturnType.FullName == "System.Void");

        var baseDispose = FindBaseDispose(type);
        if (!TypeOrAncestorDeclaresIDisposable(type))
        {
            var idisposable = module.ImportReference(typeof(IDisposable));
            type.Interfaces.Add(new InterfaceImplementation(idisposable));
        }

        if (existing is null)
        {
            var attrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            if (baseDispose is null)
                attrs |= MethodAttributes.NewSlot | MethodAttributes.Final;

            var dispose = new MethodDefinition("Dispose", attrs, module.TypeSystem.Void);
            var il = dispose.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, instanceField);
            il.Emit(OpCodes.Call, disposeHook);
            if (baseDispose is not null)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, module.ImportReference(baseDispose));
            }

            il.Emit(OpCodes.Ret);
            type.Methods.Add(dispose);
            return;
        }

        // Rename existing Dispose body and wrap (own Dispose already replaces base slot).
        var originalName = existing.Name;
        var wasNewSlot = existing.IsNewSlot;
        var wasFinal = existing.IsFinal;
        existing.Name = "__Dispose_Original";
        existing.IsPrivate = true;
        existing.IsPublic = false;
        existing.IsVirtual = false;
        existing.IsNewSlot = false;
        existing.IsFinal = false;

        var stubAttrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
        if (wasNewSlot || baseDispose is null)
            stubAttrs |= MethodAttributes.NewSlot;
        if (wasFinal && baseDispose is null)
            stubAttrs |= MethodAttributes.Final;

        var stub = new MethodDefinition(originalName, stubAttrs, module.TypeSystem.Void);
        var sil = stub.Body.GetILProcessor();
        sil.Emit(OpCodes.Ldarg_0);
        sil.Emit(OpCodes.Ldfld, instanceField);
        sil.Emit(OpCodes.Call, disposeHook);
        sil.Emit(OpCodes.Ldarg_0);
        sil.Emit(OpCodes.Call, existing);
        sil.Emit(OpCodes.Ret);
        type.Methods.Add(stub);
    }

    private static bool TypeOrAncestorDeclaresIDisposable(TypeDefinition type)
    {
        for (TypeDefinition? t = type; t is not null;)
        {
            if (t.Interfaces.Any(i => i.InterfaceType.FullName == typeof(IDisposable).FullName))
                return true;

            if (t.BaseType is null || t.BaseType.FullName == "System.Object")
                break;
            try { t = t.BaseType.Resolve(); }
            catch { break; }
        }

        return false;
    }

    private static MethodDefinition? FindBaseDispose(TypeDefinition type)
    {
        for (var baseRef = type.BaseType; baseRef is not null && baseRef.FullName != "System.Object";)
        {
            TypeDefinition? baseDef;
            try { baseDef = baseRef.Resolve(); }
            catch { break; }
            if (baseDef is null) break;

            var dispose = baseDef.Methods.FirstOrDefault(m =>
                !m.IsStatic && m.Name == nameof(IDisposable.Dispose) && !m.HasParameters &&
                m.ReturnType.FullName == "System.Void" && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly));
            if (dispose is not null)
                return dispose;

            baseRef = baseDef.BaseType;
        }

        return null;
    }
}

