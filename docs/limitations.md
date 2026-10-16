# Limitations (v1)

## Explicit non-goals

- Durable jobs / Hangfire-style persistence
- Harmony / runtime patching as the product path
- Roslyn interceptors as the primary wiring path
- GC-based shutdown
- Multiple instance handlers via inheritance (merge hierarchy later, not v1)

## AOT / trimming

The MSBuild weaver assumes a normal JIT compile + post-compile Cecil rewrite. **Native AOT / aggressive trimming of attributed libraries is not supported yet.** Prefer weaving before publish, or shipping pre-woven assemblies.

## Signature fence

By-ref, pointers, and function-pointers cannot be captured into deferred work—rejected at analyze + weave time. See [signatures](signatures.md).

## Fody

Optional Fody host was planned as fallback only; MSBuild path shipped (Fody host deferred).

## Nest static analysis

TSCH001 is best-effort. Runtime nest checks cover reflection and other paths the analyzer cannot see.
