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
    private static void RewriteMethod(
        ModuleDefinition module,
        TypeDefinition type,
        MethodDefinition method,
        FieldDefinition stateField,
        int groupId,
        bool isStatic)
    {
        var ctIndex = IndexOfCancellationToken(method);

        // Keep MethodDefinition identity for the public method so existing call sites still hit the stub.
        var original = CloneBodyToPrivateOriginal(type, method);

        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.InitLocals = false;

        var il = method.Body.GetILProcessor();
        var closedType = SelfClosedReference(type);
        var stateFieldRef = FieldOn(closedType, stateField, type, type);

        if (isStatic)
            il.Emit(OpCodes.Ldsfld, stateFieldRef);
        else
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, stateFieldRef);
        }

        il.Emit(OpCodes.Ldc_I4, groupId);
        EmitWorkDelegate(module, type, il, method, original, isStatic, ctIndex);

        MethodReference hook;
        if (ctIndex >= 0)
        {
            EmitLdarg(il, method.IsStatic, ctIndex);
            hook = ImportScheduleHook(module, method.ReturnType, isStatic, withToken: true);
        }
        else
        {
            hook = ImportScheduleHook(module, method.ReturnType, isStatic, withToken: false);
        }

        il.Emit(OpCodes.Call, hook);
        il.Emit(OpCodes.Ret);

        EnsureDebuggerStepThrough(module, method);
    }

    private static void EnsureDebuggerStepThrough(ModuleDefinition module, MethodDefinition method)
    {
        if (method.CustomAttributes.Any(a => a.AttributeType.Name == nameof(System.Diagnostics.DebuggerStepThroughAttribute)))
            return;

        var ctor = module.ImportReference(
            typeof(System.Diagnostics.DebuggerStepThroughAttribute).GetConstructor(Type.EmptyTypes)!);
        method.CustomAttributes.Add(new CustomAttribute(ctor));
    }

    private static MethodDefinition CloneBodyToPrivateOriginal(TypeDefinition type, MethodDefinition method)
    {
        var attrs = MethodAttributes.Private | MethodAttributes.HideBySig;
        if (method.IsStatic)
            attrs |= MethodAttributes.Static;

        // Return type filled after generic params exist (may reference method GPs).
        var original = new MethodDefinition(
            "__" + method.Name + "_Original",
            attrs,
            method.Module.TypeSystem.Void);
        CopyMethodGenericParameters(method, original);
        original.ReturnType = RemapTypeToOwner(method.ReturnType, method, original);
        foreach (var p in method.Parameters)
        {
            original.Parameters.Add(new ParameterDefinition(
                p.Name,
                p.Attributes,
                RemapTypeToOwner(p.ParameterType, method, original)));
        }

        // Move async/state-machine attributes onto the original implementation.
        foreach (var ca in method.CustomAttributes.ToArray())
        {
            var n = ca.AttributeType.Name;
            if (n is "AsyncStateMachineAttribute" or "IteratorStateMachineAttribute" or "DebuggerStepThroughAttribute")
            {
                method.CustomAttributes.Remove(ca);
                original.CustomAttributes.Add(ca);
            }
        }

        original.Body.InitLocals = method.Body.InitLocals;
        var varMap = new Dictionary<VariableDefinition, VariableDefinition>();
        foreach (var v in method.Body.Variables)
        {
            var mapped = new VariableDefinition(RemapTypeToOwner(v.VariableType, method, original));
            original.Body.Variables.Add(mapped);
            varMap[v] = mapped;
        }

        var il = original.Body.GetILProcessor();
        foreach (var ins in method.Body.Instructions.ToArray())
        {
            if (ins.Operand is VariableDefinition vd && varMap.TryGetValue(vd, out var mappedVar))
                ins.Operand = mappedVar;
            else
                RemapInstructionOperand(ins, method, original);
            il.Append(ins);
        }

        foreach (var eh in method.Body.ExceptionHandlers.ToArray())
            original.Body.ExceptionHandlers.Add(eh);

        type.Methods.Add(original);
        return original;
    }

    private static void CopyMethodGenericParameters(MethodDefinition from, MethodDefinition to)
    {
        foreach (var gp in from.GenericParameters)
        {
            var copy = new GenericParameter(gp.Name, to) { Attributes = gp.Attributes };
            to.GenericParameters.Add(copy);
        }

        for (var i = 0; i < from.GenericParameters.Count; i++)
        {
            foreach (var constraint in from.GenericParameters[i].Constraints)
            {
                to.GenericParameters[i].Constraints.Add(
                    new GenericParameterConstraint(RemapTypeToOwner(constraint.ConstraintType, from, to)));
            }
        }
    }

    private static void RemapInstructionOperand(
        Instruction ins,
        IGenericParameterProvider fromOwner,
        IGenericParameterProvider toOwner)
    {
        switch (ins.Operand)
        {
            case TypeReference tr:
                ins.Operand = RemapTypeToOwner(tr, fromOwner, toOwner);
                break;
            case MethodReference mr:
                ins.Operand = RemapMethodReference(mr, fromOwner, toOwner);
                break;
            case FieldReference fr:
                ins.Operand = RemapFieldReference(fr, fromOwner, toOwner);
                break;
        }
    }

    private static MethodReference RemapMethodReference(
        MethodReference method,
        IGenericParameterProvider fromOwner,
        IGenericParameterProvider toOwner)
    {
        if (method is GenericInstanceMethod gim)
        {
            var element = RemapMethodReference(gim.ElementMethod, fromOwner, toOwner);
            var neu = new GenericInstanceMethod(element);
            foreach (var arg in gim.GenericArguments)
                neu.GenericArguments.Add(RemapTypeToOwner(arg, fromOwner, toOwner));
            return neu;
        }

        var declaring = RemapTypeToOwner(method.DeclaringType, fromOwner, toOwner);
        if (ReferenceEquals(declaring, method.DeclaringType) &&
            !ContainsGenericParamOwnedBy(method.ReturnType, fromOwner) &&
            method.Parameters.All(p => !ContainsGenericParamOwnedBy(p.ParameterType, fromOwner)))
            return method;

        var mr = new MethodReference(
            method.Name,
            RemapTypeToOwner(method.ReturnType, fromOwner, toOwner),
            declaring)
        {
            HasThis = method.HasThis,
            ExplicitThis = method.ExplicitThis,
            CallingConvention = method.CallingConvention,
        };
        foreach (var p in method.Parameters)
        {
            mr.Parameters.Add(new ParameterDefinition(
                p.Name,
                p.Attributes,
                RemapTypeToOwner(p.ParameterType, fromOwner, toOwner)));
        }

        foreach (var gp in method.GenericParameters)
            mr.GenericParameters.Add(new GenericParameter(gp.Name, mr));
        return mr;
    }

    private static FieldReference RemapFieldReference(
        FieldReference field,
        IGenericParameterProvider fromOwner,
        IGenericParameterProvider toOwner)
    {
        var declaring = RemapTypeToOwner(field.DeclaringType, fromOwner, toOwner);
        var fieldType = RemapTypeToOwner(field.FieldType, fromOwner, toOwner);
        if (ReferenceEquals(declaring, field.DeclaringType) && ReferenceEquals(fieldType, field.FieldType))
            return field;
        return new FieldReference(field.Name, fieldType, declaring);
    }

    private static bool ContainsGenericParamOwnedBy(TypeReference type, IGenericParameterProvider owner)
    {
        if (type is GenericParameter gp)
            return ReferenceEquals(gp.Owner, owner);
        if (type is GenericInstanceType git)
            return git.GenericArguments.Any(a => ContainsGenericParamOwnedBy(a, owner))
                   || ContainsGenericParamOwnedBy(git.ElementType, owner);
        if (type is TypeSpecification spec)
            return ContainsGenericParamOwnedBy(spec.ElementType, owner);
        return false;
    }

    private static int IndexOfCancellationToken(MethodDefinition method)
    {
        for (var i = 0; i < method.Parameters.Count; i++)
        {
            if (method.Parameters[i].ParameterType.FullName == typeof(CancellationToken).FullName)
                return i;
        }

        return -1;
    }

    private static void EmitLdarg(ILProcessor il, bool isStatic, int parameterIndex)
    {
        var i = isStatic ? parameterIndex : parameterIndex + 1;
        switch (i)
        {
            case 0: il.Emit(OpCodes.Ldarg_0); break;
            case 1: il.Emit(OpCodes.Ldarg_1); break;
            case 2: il.Emit(OpCodes.Ldarg_2); break;
            case 3: il.Emit(OpCodes.Ldarg_3); break;
            default: il.Emit(OpCodes.Ldarg, i); break;
        }
    }

    private static bool HasUnsupportedParameter(MethodDefinition method, out string reason)
    {
        foreach (var p in method.Parameters)
        {
            if (p.ParameterType is ByReferenceType)
            {
                reason = UnsupportedParameterMessages.ByRef(p.Name);
                return true;
            }

            if (p.ParameterType.IsPointer)
            {
                reason = UnsupportedParameterMessages.Pointer(p.Name);
                return true;
            }

            if (p.ParameterType is FunctionPointerType)
            {
                reason = UnsupportedParameterMessages.FunctionPointer(p.Name);
                return true;
            }

            if (p.ParameterType is RequiredModifierType or OptionalModifierType)
            {
                reason = UnsupportedParameterMessages.UnsupportedTypeShape(p.Name);
                return true;
            }
        }

        reason = "";
        return false;
    }

    /// <summary>
    /// Leaves a Func&lt;TAwait&gt; / Func&lt;CancellationToken, TAwait&gt; on the evaluation stack
    /// where TAwait is Task, Task&lt;T&gt;, ValueTask, or ValueTask&lt;T&gt;.
    /// Captures non-token parameters in a nested display class when needed.
    /// </summary>
    private static void EmitWorkDelegate(
        ModuleDefinition module,
        TypeDefinition declaringType,
        ILProcessor il,
        MethodDefinition stub,
        MethodDefinition original,
        bool isStatic,
        int ctIndex)
    {
        var captured = GetCapturedParameterIndices(stub, ctIndex);
        if (captured.Count == 0)
        {
            EmitDirectMethodGroupFunc(module, il, stub, original, isStatic, ctIndex);
            return;
        }

        var invoke = CreateCaptureClosure(module, declaringType, stub, original, isStatic, ctIndex, captured);
        var closureType = (TypeDefinition)invoke.DeclaringType;
        var closureCtor = closureType.Methods.Single(m => m.IsConstructor && !m.IsStatic && !m.HasParameters);
        // Nested<!OuterT, !MethodT> at the stub call site.
        var closedClosure = ClosedClosureAtStubSite(closureType, declaringType, stub);
        var closureLocal = new VariableDefinition(closedClosure);
        stub.Body.Variables.Add(closureLocal);
        stub.Body.InitLocals = true;

        // Keep member signatures as declared on the nested type (its type GPs);
        // only specialize DeclaringType to Nested<!OuterT, !MethodT>.
        il.Emit(OpCodes.Newobj, MethodOnClosed(closedClosure, closureCtor));
        il.Emit(OpCodes.Stloc, closureLocal);

        if (!isStatic)
        {
            var thisField = closureType.Fields.Single(f => f.Name == "__this");
            il.Emit(OpCodes.Ldloc, closureLocal);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stfld, FieldOnClosed(closedClosure, thisField));
        }

        foreach (var paramIndex in captured)
        {
            var field = closureType.Fields.Single(f => f.Name == $"arg{paramIndex}");
            il.Emit(OpCodes.Ldloc, closureLocal);
            EmitLdarg(il, isStatic, paramIndex);
            il.Emit(OpCodes.Stfld, FieldOnClosed(closedClosure, field));
        }

        il.Emit(OpCodes.Ldloc, closureLocal);
        il.Emit(OpCodes.Ldftn, MethodOnClosed(closedClosure, invoke));
        il.Emit(OpCodes.Newobj, MakeFuncCtorForStub(module, stub, ctIndex));
    }

    private static List<int> GetCapturedParameterIndices(MethodDefinition stub, int ctIndex)
    {
        var captured = new List<int>();
        for (var i = 0; i < stub.Parameters.Count; i++)
        {
            if (ctIndex >= 0 && i == ctIndex)
                continue;
            captured.Add(i);
        }

        return captured;
    }

    private static void EmitDirectMethodGroupFunc(
        ModuleDefinition module,
        ILProcessor il,
        MethodDefinition stub,
        MethodDefinition original,
        bool isStatic,
        int ctIndex)
    {
        if (isStatic)
            il.Emit(OpCodes.Ldnull);
        else
            il.Emit(OpCodes.Ldarg_0);

        var declaring = (TypeDefinition)stub.DeclaringType;
        il.Emit(OpCodes.Ldftn, SpecializeMethod(MethodOnClosed(SelfClosedReference(declaring), original), stub));
        il.Emit(OpCodes.Newobj, MakeFuncCtorForStub(module, stub, ctIndex));
    }

    /// <summary>If <paramref name="stub"/> is generic, returns GenericInstanceMethod with stub's GPs.</summary>
    private static MethodReference SpecializeMethod(MethodReference open, MethodDefinition stub)
    {
        if (!stub.HasGenericParameters)
            return open;

        var gim = new GenericInstanceMethod(open);
        foreach (var gp in stub.GenericParameters)
            gim.GenericArguments.Add(gp);
        return gim;
    }

    private static MethodReference MakeFuncCtorForStub(ModuleDefinition module, MethodDefinition stub, int ctIndex)
    {
        if (ctIndex >= 0)
        {
            var open = module.ImportReference(typeof(Func<,>));
            var func = new GenericInstanceType(open);
            func.GenericArguments.Add(module.ImportReference(typeof(CancellationToken)));
            func.GenericArguments.Add(stub.ReturnType);
            return MakeFuncCtor(module, func);
        }

        var openUnary = module.ImportReference(typeof(Func<>));
        var unary = new GenericInstanceType(openUnary);
        unary.GenericArguments.Add(stub.ReturnType);
        return MakeFuncCtor(module, unary);
    }

    private static MethodDefinition CreateCaptureClosure(
        ModuleDefinition module,
        TypeDefinition declaringType,
        MethodDefinition stub,
        MethodDefinition original,
        bool isStatic,
        int ctIndex,
        List<int> captured)
    {
        var closureName = $"<>c__GS_{stub.Name}_{stub.MetadataToken.RID}";
        var existing = declaringType.NestedTypes.FirstOrDefault(t => t.Name == closureName);
        if (existing is not null)
            return existing.Methods.Single(m => m.Name == "Invoke");

        var closure = new TypeDefinition(
            "",
            closureName,
            TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit |
            TypeAttributes.Class,
            module.TypeSystem.Object);
        // Type GPs first, then method GPs (display-class shape for generic methods).
        CopyGenericParameters(declaringType, closure);
        AppendMethodGenericParameters(stub, closure);
        declaringType.NestedTypes.Add(closure);

        FieldDefinition? thisField = null;
        if (!isStatic)
        {
            thisField = new FieldDefinition(
                "__this",
                FieldAttributes.Public,
                ClosedDeclaringFromClosure(declaringType, closure));
            closure.Fields.Add(thisField);
        }

        var argFields = new Dictionary<int, FieldDefinition>();
        foreach (var paramIndex in captured)
        {
            var p = stub.Parameters[paramIndex];
            var fieldType = RemapToClosure(p.ParameterType, declaringType, stub, closure);
            var field = new FieldDefinition($"arg{paramIndex}", FieldAttributes.Public, fieldType);
            closure.Fields.Add(field);
            argFields[paramIndex] = field;
        }

        var ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        var ctorIl = ctor.Body.GetILProcessor();
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Call, module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes)!));
        ctorIl.Emit(OpCodes.Ret);
        closure.Methods.Add(ctor);

        var invokeReturn = RemapToClosure(stub.ReturnType, declaringType, stub, closure);
        var invoke = new MethodDefinition(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig,
            invokeReturn);
        if (ctIndex >= 0)
        {
            invoke.Parameters.Add(new ParameterDefinition(
                "cancellationToken",
                ParameterAttributes.None,
                module.ImportReference(typeof(CancellationToken))));
        }

        var invokeIl = invoke.Body.GetILProcessor();
        var closedClosure = SelfClosedReference(closure);
        if (!isStatic)
        {
            invokeIl.Emit(OpCodes.Ldarg_0);
            invokeIl.Emit(OpCodes.Ldfld, FieldOnClosed(closedClosure, thisField!));
        }

        for (var i = 0; i < stub.Parameters.Count; i++)
        {
            if (ctIndex >= 0 && i == ctIndex)
            {
                // Linked token passed into Invoke(CancellationToken).
                invokeIl.Emit(OpCodes.Ldarg_1);
            }
            else
            {
                invokeIl.Emit(OpCodes.Ldarg_0);
                invokeIl.Emit(OpCodes.Ldfld, FieldOnClosed(closedClosure, argFields[i]));
            }
        }

        var closedDeclaring = ClosedDeclaringFromClosure(declaringType, closure);
        var originalCall = MethodOnClosed(closedDeclaring, original);
        // Specialize Original<!MethodT> using the trailing closure GPs that mirror stub GPs.
        if (stub.HasGenericParameters)
        {
            var gim = new GenericInstanceMethod(originalCall);
            var offset = declaringType.GenericParameters.Count;
            for (var i = 0; i < stub.GenericParameters.Count; i++)
                gim.GenericArguments.Add(closure.GenericParameters[offset + i]);
            originalCall = gim;
        }

        invokeIl.Emit(OpCodes.Call, originalCall);
        invokeIl.Emit(OpCodes.Ret);
        closure.Methods.Add(invoke);
        return invoke;
    }

    private static void CopyGenericParameters(TypeDefinition from, TypeDefinition to)
    {
        foreach (var gp in from.GenericParameters)
        {
            var copy = new GenericParameter(gp.Name, to) { Attributes = gp.Attributes };
            to.GenericParameters.Add(copy);
        }

        for (var i = 0; i < from.GenericParameters.Count; i++)
        {
            foreach (var constraint in from.GenericParameters[i].Constraints)
                to.GenericParameters[i].Constraints.Add(new GenericParameterConstraint(constraint.ConstraintType));
        }
    }

    private static void AppendMethodGenericParameters(MethodDefinition method, TypeDefinition closure)
    {
        var start = closure.GenericParameters.Count;
        foreach (var gp in method.GenericParameters)
        {
            var copy = new GenericParameter(gp.Name, closure) { Attributes = gp.Attributes };
            closure.GenericParameters.Add(copy);
        }

        for (var i = 0; i < method.GenericParameters.Count; i++)
        {
            foreach (var constraint in method.GenericParameters[i].Constraints)
            {
                closure.GenericParameters[start + i].Constraints.Add(
                    new GenericParameterConstraint(
                        RemapToClosure(constraint.ConstraintType, (TypeDefinition)method.DeclaringType, method, closure)));
            }
        }
    }

    /// <summary>DeclaringType&lt;!closureTypeGPs…&gt; using only the leading GPs that mirror the outer type.</summary>
    private static TypeReference ClosedDeclaringFromClosure(TypeDefinition declaringType, TypeDefinition closure)
    {
        if (!declaringType.HasGenericParameters)
            return declaringType;

        var git = new GenericInstanceType(declaringType);
        for (var i = 0; i < declaringType.GenericParameters.Count; i++)
            git.GenericArguments.Add(closure.GenericParameters[i]);
        return git;
    }

    private static TypeReference ClosedClosureAtStubSite(
        TypeDefinition closure,
        TypeDefinition declaringType,
        MethodDefinition stub)
    {
        if (!closure.HasGenericParameters)
            return closure;

        var git = new GenericInstanceType(closure);
        foreach (var gp in declaringType.GenericParameters)
            git.GenericArguments.Add(gp);
        foreach (var gp in stub.GenericParameters)
            git.GenericArguments.Add(gp);
        return git;
    }

    private static TypeReference RemapToClosure(
        TypeReference type,
        TypeDefinition declaringType,
        MethodDefinition stub,
        TypeDefinition closure)
    {
        if (type is GenericParameter gp)
        {
            if (ReferenceEquals(gp.Owner, declaringType))
                return closure.GenericParameters[gp.Position];
            if (ReferenceEquals(gp.Owner, stub))
                return closure.GenericParameters[declaringType.GenericParameters.Count + gp.Position];
            return gp;
        }

        if (type is GenericInstanceType git)
        {
            var element = git.ElementType is TypeSpecification or GenericParameter
                ? RemapToClosure(git.ElementType, declaringType, stub, closure)
                : git.ElementType;
            var mapped = new GenericInstanceType(element);
            foreach (var arg in git.GenericArguments)
                mapped.GenericArguments.Add(RemapToClosure(arg, declaringType, stub, closure));
            return mapped;
        }

        if (type is ArrayType array)
            return new ArrayType(RemapToClosure(array.ElementType, declaringType, stub, closure), array.Rank);

        if (type is ByReferenceType byRef)
            return new ByReferenceType(RemapToClosure(byRef.ElementType, declaringType, stub, closure));

        return type;
    }

    private static TypeReference RemapFromClosure(
        TypeReference type,
        TypeDefinition closure,
        TypeDefinition declaringType,
        MethodDefinition stub)
    {
        if (type is GenericParameter gp && ReferenceEquals(gp.Owner, closure))
        {
            if (gp.Position < declaringType.GenericParameters.Count)
                return declaringType.GenericParameters[gp.Position];
            return stub.GenericParameters[gp.Position - declaringType.GenericParameters.Count];
        }

        if (type is GenericInstanceType git)
        {
            var element = git.ElementType is TypeSpecification or GenericParameter
                ? RemapFromClosure(git.ElementType, closure, declaringType, stub)
                : git.ElementType;
            var mapped = new GenericInstanceType(element);
            foreach (var arg in git.GenericArguments)
                mapped.GenericArguments.Add(RemapFromClosure(arg, closure, declaringType, stub));
            return mapped;
        }

        if (type is ArrayType array)
            return new ArrayType(RemapFromClosure(array.ElementType, closure, declaringType, stub), array.Rank);

        if (type is ByReferenceType byRef)
            return new ByReferenceType(RemapFromClosure(byRef.ElementType, closure, declaringType, stub));

        return type;
    }

    private static FieldReference FieldOnClosed(TypeReference declaringType, FieldDefinition field)
    {
        if (!declaringType.IsGenericInstance &&
            (ReferenceEquals(field.DeclaringType, declaringType) || declaringType == field.DeclaringType))
            return field;

        return new FieldReference(field.Name, field.FieldType, declaringType);
    }

    /// <summary>Outer&lt;!0&gt; expressed with <paramref name="genericOwner"/>'s generic parameters.</summary>
    private static TypeReference SelfClosedReference(TypeDefinition openType, IGenericParameterProvider genericOwner)
    {
        if (!openType.HasGenericParameters)
            return openType;

        var git = new GenericInstanceType(openType);
        var count = openType.GenericParameters.Count;
        var ownerParams = genericOwner.GenericParameters;
        // When owner is a method-closure with extra GPs, only take the leading ones matching openType.
        for (var i = 0; i < count; i++)
            git.GenericArguments.Add(ownerParams[i]);
        return git;
    }

    private static TypeReference RemapTypeToOwner(
        TypeReference type,
        IGenericParameterProvider fromOwner,
        IGenericParameterProvider toOwner)
    {
        if (type is GenericParameter gp && ReferenceEquals(gp.Owner, fromOwner))
            return toOwner.GenericParameters[gp.Position];

        if (type is GenericInstanceType git)
        {
            var element = git.ElementType is TypeSpecification or GenericParameter
                ? RemapTypeToOwner(git.ElementType, fromOwner, toOwner)
                : git.ElementType;
            var mapped = new GenericInstanceType(element);
            foreach (var arg in git.GenericArguments)
                mapped.GenericArguments.Add(RemapTypeToOwner(arg, fromOwner, toOwner));
            return mapped;
        }

        if (type is ArrayType array)
            return new ArrayType(RemapTypeToOwner(array.ElementType, fromOwner, toOwner), array.Rank);

        if (type is ByReferenceType byRef)
            return new ByReferenceType(RemapTypeToOwner(byRef.ElementType, fromOwner, toOwner));

        return type;
    }

    /// <summary>
    /// MemberRef to <paramref name="method"/> on a closed/self GenericInstance declaring type,
    /// preserving the method's declared signature (type GPs stay owned by the open type def).
    /// </summary>
    private static MethodReference MethodOnClosed(TypeReference declaringType, MethodDefinition method)
    {
        if (!declaringType.IsGenericInstance &&
            (ReferenceEquals(method.DeclaringType, declaringType) || declaringType == method.DeclaringType))
            return method;

        var mr = new MethodReference(method.Name, method.ReturnType, declaringType)
        {
            HasThis = method.HasThis,
            ExplicitThis = method.ExplicitThis,
            CallingConvention = method.CallingConvention,
        };
        foreach (var p in method.Parameters)
            mr.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, p.ParameterType));
        foreach (var gp in method.GenericParameters)
            mr.GenericParameters.Add(new GenericParameter(gp.Name, mr));
        return mr;
    }

    private static MethodReference MethodOn(
        TypeReference declaringType,
        MethodDefinition method,
        IGenericParameterProvider fromOwner,
        IGenericParameterProvider toOwner)
    {
        if (!fromOwner.HasGenericParameters &&
            (ReferenceEquals(method.DeclaringType, declaringType) || declaringType == method.DeclaringType))
            return method;

        // Prefer MethodOnClosed for same-type specialization; remap only when owners differ (nested closures).
        if (ReferenceEquals(fromOwner, toOwner))
            return MethodOnClosed(declaringType, method);

        var mr = new MethodReference(
            method.Name,
            RemapTypeToOwner(method.ReturnType, fromOwner, toOwner),
            declaringType)
        {
            HasThis = method.HasThis,
            ExplicitThis = method.ExplicitThis,
            CallingConvention = method.CallingConvention,
        };
        foreach (var p in method.Parameters)
        {
            mr.Parameters.Add(new ParameterDefinition(
                p.Name,
                p.Attributes,
                RemapTypeToOwner(p.ParameterType, fromOwner, toOwner)));
        }

        foreach (var gp in method.GenericParameters)
            mr.GenericParameters.Add(new GenericParameter(gp.Name, mr));
        return mr;
    }

    private static FieldReference FieldOn(
        TypeReference declaringType,
        FieldDefinition field,
        IGenericParameterProvider fromOwner,
        IGenericParameterProvider toOwner)
    {
        if (!fromOwner.HasGenericParameters &&
            (ReferenceEquals(field.DeclaringType, declaringType) || declaringType == field.DeclaringType))
            return field;

        return new FieldReference(
            field.Name,
            RemapTypeToOwner(field.FieldType, fromOwner, toOwner),
            declaringType);
    }

    private static MethodReference MakeFuncCtor(ModuleDefinition module, GenericInstanceType funcType)
    {
        var ctor = new MethodReference(".ctor", module.TypeSystem.Void, funcType) { HasThis = true };
        ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.IntPtr));
        return ctor;
    }

    private static MethodReference ImportScheduleHook(
        ModuleDefinition module,
        TypeReference returnType,
        bool isStatic,
        bool withToken)
    {
        var name = isStatic ? nameof(SchedulingWeaveHooks.ScheduleStatic) : nameof(SchedulingWeaveHooks.Schedule);
        var methods = typeof(SchedulingWeaveHooks).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == name);
        var kind = ClassifyAwaitable(returnType);
        var arity = withToken ? 4 : 3;

        MethodInfo chosen;
        if (kind is AwaitableKind.TaskOfT or AwaitableKind.ValueTaskOfT)
        {
            var git = (GenericInstanceType)returnType;
            chosen = methods.Single(m =>
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == arity &&
                FuncResultMatches(m.GetParameters()[2].ParameterType, withToken, kind));
            var open = module.ImportReference(chosen);
            var closed = new GenericInstanceMethod(open);
            closed.GenericArguments.Add(git.GenericArguments[0]);
            return closed;
        }

        chosen = methods.Single(m =>
            !m.IsGenericMethodDefinition &&
            m.GetParameters().Length == arity &&
            FuncResultMatches(m.GetParameters()[2].ParameterType, withToken, kind));
        return module.ImportReference(chosen);
    }

    private enum AwaitableKind
    {
        Task,
        TaskOfT,
        ValueTask,
        ValueTaskOfT,
    }

    private static AwaitableKind ClassifyAwaitable(TypeReference returnType)
    {
        if (returnType.FullName == "System.Threading.Tasks.Task")
            return AwaitableKind.Task;
        if (returnType.FullName == "System.Threading.Tasks.ValueTask")
            return AwaitableKind.ValueTask;
        if (returnType is GenericInstanceType git)
        {
            if (git.ElementType.FullName == "System.Threading.Tasks.Task`1")
                return AwaitableKind.TaskOfT;
            if (git.ElementType.FullName == "System.Threading.Tasks.ValueTask`1")
                return AwaitableKind.ValueTaskOfT;
        }

        throw new InvalidOperationException($"Unsupported awaitable return type '{returnType.FullName}'.");
    }

    private static bool FuncResultMatches(Type funcType, bool withToken, AwaitableKind kind)
    {
        if (!funcType.IsGenericType)
            return false;

        var args = funcType.GetGenericArguments();
        var result = withToken ? args[1] : args[0];
        return kind switch
        {
            AwaitableKind.Task => result == typeof(Task),
            AwaitableKind.ValueTask => result == typeof(ValueTask),
            AwaitableKind.TaskOfT => result.IsGenericType && result.GetGenericTypeDefinition() == typeof(Task<>),
            AwaitableKind.ValueTaskOfT => result.IsGenericType && result.GetGenericTypeDefinition() == typeof(ValueTask<>),
            _ => false,
        };
    }
}

