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

/// <summary>
/// Host-agnostic Mono.Cecil transforms for <see cref="ScheduleGroupAttribute"/>.
/// </summary>
public static partial class SchedulingModuleWeaver
{
    public static WeaveResult Weave(ModuleDefinition module, WeaveOptions? options = null)
    {
        // Reserved for future weave knobs (attribute name overrides, etc.).
        _ = options;

        var diagnostics = new List<WeaveDiagnostic>();
        var woven = 0;
        var scheduleAttrRef = module.ImportReference(typeof(ScheduleGroupAttribute));

        foreach (var type in module.Types.ToArray())
            woven += WeaveTypeTree(module, type, scheduleAttrRef, diagnostics);

        return new WeaveResult(diagnostics, woven);
    }

    private static int WeaveTypeTree(
        ModuleDefinition module,
        TypeDefinition type,
        TypeReference scheduleAttrRef,
        List<WeaveDiagnostic> diagnostics)
    {
        var woven = 0;
        if (!type.IsInterface && !type.IsEnum && !type.Name.Contains('<'))
        {
            try
            {
                woven += WeaveType(module, type, scheduleAttrRef, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(Error("GSWEAVE000", $"Failed weaving '{type.FullName}': {ex.Message}"));
            }
        }

        foreach (var nested in type.NestedTypes.ToArray())
            woven += WeaveTypeTree(module, nested, scheduleAttrRef, diagnostics);

        return woven;
    }

    private static int WeaveType(
        ModuleDefinition module,
        TypeDefinition type,
        TypeReference scheduleAttrRef,
        List<WeaveDiagnostic> diagnostics)
    {
        var attributed = type.Methods
            .Where(m => !m.IsConstructor && m.HasBody && m.HasCustomAttributes)
            .Select(m => (Method: m, Attr: FindScheduleAttribute(m, scheduleAttrRef)))
            .Where(x => x.Attr is not null)
            // Idempotent: skip stubs already rewritten (private __Name_Original companion exists).
            .Where(x => type.Methods.All(m => m.Name != "__" + x.Method.Name + "_Original"))
            .Select(x => (x.Method, Attr: x.Attr!))
            .ToList();

        if (attributed.Count == 0)
            return 0;

        var instanceMethods = attributed.Where(x => !x.Method.IsStatic).ToList();
        var staticMethods = attributed.Where(x => x.Method.IsStatic).ToList();

        if (instanceMethods.Count > 0 && HasAttributedInstanceAncestor(type, scheduleAttrRef))
        {
            diagnostics.Add(Error("GSWEAVE002",
                $"Type '{type.FullName}' has [ScheduleGroup] instance methods but a base type already does."));
            return 0;
        }

        foreach (var (method, _) in attributed)
        {
            if (!IsSupportedAwaitable(method.ReturnType, out _))
            {
                diagnostics.Add(Error("GSWEAVE004",
                    $"Method '{type.FullName}.{method.Name}' must return Task, Task<T>, ValueTask, or ValueTask<T>."));
                return 0;
            }

            if (HasUnsupportedParameter(method, out var badParamReason))
            {
                diagnostics.Add(Error("GSWEAVE005",
                    $"Method '{type.FullName}.{method.Name}' has unsupported parameters: {badParamReason}"));
                return 0;
            }
        }

        Dictionary<string, GroupInfo>? instanceGroups = null;
        Dictionary<string, GroupInfo>? staticGroups = null;
        FieldDefinition? instanceField = null;
        FieldDefinition? staticField = null;

        if (instanceMethods.Count > 0)
        {
            instanceGroups = BuildGroupMap(instanceMethods);
            instanceField = EnsureInstanceStateField(module, type);
            var create = EnsureCreateStateMethod(module, type, SchedulingWeaveHooks.CreateInstanceStateMethodName,
                typeof(InstanceSchedulingState), instanceGroups);
            WeaveInstanceConstructors(module, type, instanceField, create);
            EnsureDispose(module, type, instanceField);
        }

        if (staticMethods.Count > 0)
        {
            staticGroups = BuildGroupMap(staticMethods);
            staticField = EnsureStaticStateField(module, type);
            var create = EnsureCreateStateMethod(module, type, SchedulingWeaveHooks.CreateStaticStateMethodName,
                typeof(StaticSchedulingState), staticGroups);
            EnsureStaticInitializer(module, type, staticField, create);
        }

        var woven = 0;
        foreach (var (method, attr) in instanceMethods)
        {
            RewriteMethod(module, type, method, instanceField!, instanceGroups![attr.Name].Id, isStatic: false);
            woven++;
        }

        foreach (var (method, attr) in staticMethods)
        {
            RewriteMethod(module, type, method, staticField!, staticGroups![attr.Name].Id, isStatic: true);
            woven++;
        }

        return woven;
    }

    private static bool HasAttributedInstanceAncestor(TypeDefinition type, TypeReference scheduleAttrRef)
    {
        for (var baseRef = type.BaseType; baseRef is not null && baseRef.FullName != "System.Object";)
        {
            TypeDefinition? baseDef;
            try { baseDef = baseRef.Resolve(); }
            catch { break; }
            if (baseDef is null) break;

            if (baseDef.Fields.Any(f => f.Name == SchedulingWeaveHooks.InstanceStateFieldName))
                return true;
            if (baseDef.Methods.Any(m => !m.IsStatic && !m.IsConstructor && FindScheduleAttribute(m, scheduleAttrRef) is not null))
                return true;

            baseRef = baseDef.BaseType;
        }

        return false;
    }

    private sealed record GroupInfo(int Id, string Name, ScheduleMode Mode);
    private sealed record ScheduleAttr(string Name, ScheduleMode Mode);

    private static Dictionary<string, GroupInfo> BuildGroupMap(List<(MethodDefinition Method, ScheduleAttr Attr)> methods)
    {
        var map = new Dictionary<string, GroupInfo>(StringComparer.Ordinal);
        var next = 1;
        foreach (var (_, attr) in methods)
        {
            if (map.TryGetValue(attr.Name, out var existing))
            {
                if (existing.Mode != attr.Mode)
                    throw new InvalidOperationException($"Conflicting modes for group '{attr.Name}'.");
                continue;
            }

            map[attr.Name] = new GroupInfo(next++, attr.Name, attr.Mode);
        }

        return map;
    }

    private static ScheduleAttr? FindScheduleAttribute(MethodDefinition method, TypeReference scheduleAttrRef)
    {
        foreach (var ca in method.CustomAttributes)
        {
            if (ca.AttributeType.FullName != scheduleAttrRef.FullName)
                continue;
            if (ca.ConstructorArguments.Count < 2)
                return null;

            var name = ca.ConstructorArguments[0].Value as string;
            var modeVal = ca.ConstructorArguments[1].Value;
            var mode = (ScheduleMode)Convert.ToInt32(modeVal);
            if (string.IsNullOrWhiteSpace(name))
                return null;
            return new ScheduleAttr(name, mode);
        }

        return null;
    }

    private static bool IsSupportedAwaitable(TypeReference type, out TypeReference? tArg)
    {
        tArg = null;
        if (type.FullName is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask")
            return true;
        if (type is GenericInstanceType git &&
            git.ElementType.FullName is "System.Threading.Tasks.Task`1" or "System.Threading.Tasks.ValueTask`1")
        {
            tArg = git.GenericArguments[0];
            return true;
        }

        return false;
    }

    private static FieldDefinition EnsureInstanceStateField(ModuleDefinition module, TypeDefinition type)
    {
        var existing = type.Fields.FirstOrDefault(f => f.Name == SchedulingWeaveHooks.InstanceStateFieldName);
        if (existing is not null)
            return existing;

        var field = new FieldDefinition(
            SchedulingWeaveHooks.InstanceStateFieldName,
            FieldAttributes.Private,
            module.ImportReference(typeof(InstanceSchedulingState)));
        type.Fields.Add(field);
        return field;
    }

    private static FieldDefinition EnsureStaticStateField(ModuleDefinition module, TypeDefinition type)
    {
        var existing = type.Fields.FirstOrDefault(f => f.Name == SchedulingWeaveHooks.StaticStateFieldName);
        if (existing is not null)
            return existing;

        var field = new FieldDefinition(
            SchedulingWeaveHooks.StaticStateFieldName,
            FieldAttributes.Private | FieldAttributes.Static,
            module.ImportReference(typeof(StaticSchedulingState)));
        type.Fields.Add(field);
        return field;
    }

    private static MethodDefinition EnsureCreateStateMethod(
        ModuleDefinition module,
        TypeDefinition type,
        string methodName,
        Type stateClrType,
        Dictionary<string, GroupInfo> groups)
    {
        var existing = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (existing is not null)
            return existing;

        var stateType = module.ImportReference(stateClrType);
        var dictType = module.ImportReference(typeof(Dictionary<int, EventActionGroup>));
        var dictCtor = module.ImportReference(typeof(Dictionary<int, EventActionGroup>).GetConstructor(Type.EmptyTypes)!);
        var dictAdd = module.ImportReference(typeof(Dictionary<int, EventActionGroup>).GetMethod("Add")!);
        var groupCtor = module.ImportReference(typeof(EventActionGroup).GetConstructor([typeof(string), typeof(ScheduleMode)])!);
        var stateCtor = module.ImportReference(stateClrType.GetConstructor([typeof(Dictionary<int, EventActionGroup>)])!);

        var method = new MethodDefinition(
            methodName,
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
            stateType);
        type.Methods.Add(method);

        var il = method.Body.GetILProcessor();
        var local = new VariableDefinition(dictType);
        method.Body.Variables.Add(local);
        method.Body.InitLocals = true;

        il.Emit(OpCodes.Newobj, dictCtor);
        il.Emit(OpCodes.Stloc, local);
        foreach (var g in groups.Values.OrderBy(x => x.Id))
        {
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ldc_I4, g.Id);
            il.Emit(OpCodes.Ldstr, g.Name);
            il.Emit(OpCodes.Ldc_I4, (int)g.Mode);
            il.Emit(OpCodes.Newobj, groupCtor);
            il.Emit(OpCodes.Callvirt, dictAdd);
        }

        il.Emit(OpCodes.Ldloc, local);
        il.Emit(OpCodes.Newobj, stateCtor);
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static void WeaveInstanceConstructors(
        ModuleDefinition module,
        TypeDefinition type,
        FieldDefinition instanceField,
        MethodDefinition createState)
    {
        var init = module.ImportReference(typeof(SchedulingWeaveHooks).GetMethod(nameof(SchedulingWeaveHooks.InitInstanceState))!);
        var funcCtor = module.ImportReference(typeof(Func<InstanceSchedulingState>).GetConstructor([typeof(object), typeof(IntPtr)])!);
        var closedType = SelfClosedReference(type);
        var createRef = MethodOn(closedType, createState, type, type);
        var fieldRef = FieldOn(closedType, instanceField, type, type);

        foreach (var ctor in type.GetConstructors().Where(c => c.HasBody && !c.IsStatic))
        {
            if (CallsThisConstructor(ctor, type))
                continue;

            var il = ctor.Body.GetILProcessor();
            var afterBase = FindAfterBaseCtorCall(ctor);
            Instruction[] prologue =
            [
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Ldnull),
                il.Create(OpCodes.Ldftn, createRef),
                il.Create(OpCodes.Newobj, funcCtor),
                il.Create(OpCodes.Call, init),
                il.Create(OpCodes.Stfld, fieldRef),
            ];

            for (var i = prologue.Length - 1; i >= 0; i--)
                il.InsertAfter(afterBase, prologue[i]);
        }
    }

    private static bool CallsThisConstructor(MethodDefinition ctor, TypeDefinition type)
    {
        foreach (var ins in ctor.Body.Instructions)
        {
            if (ins.Operand is MethodReference { Name: ".ctor" } mr &&
                mr.DeclaringType.FullName == type.FullName)
                return true;
        }

        return false;
    }

    private static Instruction FindAfterBaseCtorCall(MethodDefinition ctor)
    {
        foreach (var ins in ctor.Body.Instructions)
        {
            if (ins.OpCode == OpCodes.Call && ins.Operand is MethodReference { Name: ".ctor" })
                return ins;
        }

        return ctor.Body.Instructions[0];
    }

    private static void EnsureStaticInitializer(
        ModuleDefinition module,
        TypeDefinition type,
        FieldDefinition staticField,
        MethodDefinition createState)
    {
        var cctor = type.GetStaticConstructor();
        if (cctor is null)
        {
            cctor = new MethodDefinition(
                ".cctor",
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            var cil = cctor.Body.GetILProcessor();
            cil.Emit(OpCodes.Ret);
            type.Methods.Add(cctor);
        }

        var getOrCreate = module.ImportReference(
            typeof(HandlerRegistry).GetMethod(nameof(HandlerRegistry.GetOrCreateStaticState))!);
        var getTypeFromHandle = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
        var funcCtor = module.ImportReference(
            typeof(Func<StaticSchedulingState>).GetConstructor([typeof(object), typeof(IntPtr)])!);

        // field = HandlerRegistry.GetOrCreateStaticState(typeof(T), createState);
        // InsertBefore + forward order keeps sequence intact (unlike InsertAfter + reverse).
        var il = cctor.Body.GetILProcessor();
        var first = cctor.Body.Instructions[0];
        // typeof(Foo<T>) at the closed runtime type — not the open definition.
        var typeToken = SelfClosedReference(type);
        var createRef = MethodOn(typeToken, createState, type, type);
        var fieldRef = FieldOn(typeToken, staticField, type, type);
        Instruction[] prologue =
        [
            il.Create(OpCodes.Ldtoken, typeToken),
            il.Create(OpCodes.Call, getTypeFromHandle),
            il.Create(OpCodes.Ldnull),
            il.Create(OpCodes.Ldftn, createRef),
            il.Create(OpCodes.Newobj, funcCtor),
            il.Create(OpCodes.Call, getOrCreate),
            il.Create(OpCodes.Stsfld, fieldRef),
        ];
        foreach (var ins in prologue)
            il.InsertBefore(first, ins);
    }

    /// <summary>
    /// For open generic type definitions, returns <c>Foo&lt;!0,…&gt;</c> so ldtoken/ldfld bind to the closed type at runtime.
    /// </summary>
    private static TypeReference SelfClosedReference(TypeDefinition type)
    {
        if (!type.HasGenericParameters)
            return type;

        var git = new GenericInstanceType(type);
        foreach (var gp in type.GenericParameters)
            git.GenericArguments.Add(gp);
        return git;
    }

    private static WeaveDiagnostic Error(string id, string message)
        => new(WeaveSeverity.Error, id, message);
}
