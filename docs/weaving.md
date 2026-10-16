# Weaving

Primary host: **raw Mono.Cecil MSBuild task** (`GroupedScheduling.Build`). Shared logic: `GroupedScheduling.Weaver.SchedulingModuleWeaver`. Fody fallback is deferred (MSBuild path works).

## What the weaver does

For each type with attributed methods:

1. **Validate** returns, parameters, inheritance, group mode conflicts.
2. **Inject state**
   - Instance: `__groupedSchedulingState` + ctor prologue via `InitInstanceState` / `__GS_CreateInstanceState`
   - Static: `__groupedSchedulingStaticState` + `__GS_CreateStaticState` (lazy via registry)
3. **Rewrite methods** — original body moved to `__Name_Original`; public stub schedules through `SchedulingWeaveHooks.Schedule*` / `ScheduleStatic*`
4. **Captures** — non-token args go into a display class / `Func` so work can run after the lane grants
5. **Dispose** — ensure `IDisposable` on types with instance attributed methods; dispose shuts instance state first, then any prior/`base` Dispose. **Static** handlers are not disposed via type `IDisposable` — process stop walks `HandlerRegistry`.
6. **Generics** — declaring-type GPs and method GPs are remapped onto `__Original` and closures

Wrappers are marked `[DebuggerStepThrough]`. Prefer breakpoints in your source method body.

## MSBuild

`build/GroupedScheduling.Build.targets`:

- Target `GroupedSchedulingWeave` runs after `CoreCompile`, before copy to output
- Skips design-time builds
- Incremental stamp: `*.groupedscheduling.stamp`
- Property: `GroupedSchedulingWeaveEnabled` (default `true`)

## Idempotency

If `__Name_Original` already exists, that method is skipped (safe re-weave / already processed).

## Debug tip

Stack traces may show `__SaveAsync_Original`. Step-into may briefly hit the thin wrapper first.
