# Method signatures

## Supported

| Aspect | Allowed |
|---|---|
| Return type | `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` (including tuples as `T`) |
| Declaring type | Non-generic or generic (`EntityStore<T>`) |
| Method | Non-generic or generic (`AcceptAsync<T>`) |
| Parameters | Ordinary values + optional `CancellationToken` |
| Kind | Instance or `static` |

## Unsupported (TSCH004 / GSWEAVE004–005)

| Aspect | Why |
|---|---|
| `void` / sync returns | Nothing to schedule as an awaitable |
| `ref` / `out` / `in` parameters | Cannot capture into a deferred `Func` / display class |
| Pointers / function pointers | Same capture problem |
| modreq / modopt shapes | Rejected by weaver as unsupported type shape |

Shared reason strings live in [`Shared/UnsupportedParameterMessages.cs`](../Shared/UnsupportedParameterMessages.cs) (linked into Analyzers + Weaver). Detection stays dual (Roslyn vs Cecil).

## Inheritance

Only **one level** in a type hierarchy may declare instance `[ScheduleGroup]` methods (single injected instance field). Base + derived both attributing instance methods → **TSCH003** / **GSWEAVE002**.

Static attributed methods on base vs derived remain separate static handlers per declaring type.
