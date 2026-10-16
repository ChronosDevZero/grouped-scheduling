# GroupedScheduling sample

Project docs: [`docs/`](../../docs/README.md) · this sample: [`docs/samples-and-tests.md`](../../docs/samples-and-tests.md)

Console host that:

1. Imports the in-repo weaver targets (NuGet consumers use `GroupedScheduling.Build` instead).
2. Registers `AddGroupedScheduling()` for process-level shutdown.
3. Runs smoke modules against several real woven workers.

| Smoke | What it proves |
|-------|----------------|
| `ArgAndNestedSmoke` | Business args + nested type weave |
| `ValueTaskSmoke` | `ValueTask` / `ValueTask<T>` + `Task<(T1,T2)>` |
| `GenericsSmoke` | `EntityStore<T>` + `AcceptAsync<T>` / static `CountAsync<T>` |
| `CombinedGenericsSmoke` | `Mapper<TIn>.MapAsync<TOut>` (type + method generics) |
| `StaticOnlySmoke` | Static-only worker (no instance methods) |
| `NestRejectSmoke` | Same-instance nest throws at runtime |
| `CrossInstanceNestSmoke` | A→B nest across instances is allowed |
| `CancelInFlightSmoke` | Method `CancellationToken` cancels in-flight work |
| `ExceptionPropagateSmoke` | Exception reaches caller; exclusive lane frees for sibling |
| `ParallelOverlapSmoke` | ExclusiveParallel methods *do* overlap |
| `ModeRoundsSmoke` | Four mode rounds + burst; serial intervals never overlap |
| `HostStopSmoke` | Mid-run `StopAsync` cancels hold+queued; post-stop rejects new work |

```bash
dotnet run --project samples/GroupedScheduling.Sample
```
