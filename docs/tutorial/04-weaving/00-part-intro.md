# Part III — Weaving

This is the largest part of the book. Everything you learned about `SchedulingWeaveHooks` and `ActionsHandler` is what the weaver **emits calls to**.

## Projects

| Project | Role |
|---|---|
| `GroupedScheduling.Weaver` | Host-agnostic Mono.Cecil transforms (`SchedulingModuleWeaver`) |
| `GroupedScheduling.Build` | MSBuild task + NuGet packaging that runs the weaver after `CoreCompile` |

## Chapters in order

1. Why body wrapping (delegates / reflection).
2. Result / options / diagnostics types.
3. Entry + discovery (`SchedulingModuleWeaver.cs`) — validate, fields, ctors, group map.
4. Rewrite (`Rewrite.cs`) — stub, `__Original`, closures, hook import.
5. Dispose injection (`Dispose.cs`).
6. Assembly I/O (`AssemblyIO.cs`).
7. MSBuild task + targets + packaging.

## Pipeline reminder

```text
  CSC -> Intermediate.dll
           |
           v
  GroupedSchedulingWeave (MSBuild)
           |
           v
  SchedulingModuleWeaver.WeaveAssembly
           |
           v
  per type: validate -> inject state -> rewrite methods -> dispose
           |
           v
  write DLL (+ PDB) in place
```

## Debug tip

Wrappers get `[DebuggerStepThrough]`. Stack traces may show `__SaveAsync_Original`. Prefer breakpoints in your source method body.
