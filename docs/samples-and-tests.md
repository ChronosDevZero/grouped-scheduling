# Samples & tests

## Sample (`samples/GroupedScheduling.Sample`)

Console host with `AddGroupedScheduling()` and modular smokes:

| Smoke | Proves |
|---|---|
| `ArgAndNestedSmoke` | Business args + nested type weave |
| `ValueTaskSmoke` | `ValueTask` / `ValueTask<T>` + `Task<(T1,T2)>` |
| `GenericsSmoke` | `EntityStore<T>` + generic methods |
| `CombinedGenericsSmoke` | `Mapper<TIn>.MapAsync<TOut>` |
| `StaticOnlySmoke` | Static-only worker |
| `NestRejectSmoke` | Same-instance nest throws (reflection path) |
| `CrossInstanceNestSmoke` | A→B across instances OK |
| `CancelInFlightSmoke` | Method token cancels in-flight work |
| `ExceptionPropagateSmoke` | Exception to caller; exclusive lane frees |
| `ParallelOverlapSmoke` | ExclusiveParallel overlap |
| `ModeRoundsSmoke` | Mode rounds + serial non-overlap |
| `HostStopSmoke` | Mid-run stop + post-stop reject |

```bash
dotnet run --project samples/GroupedScheduling.Sample
```

## Fixture (`GroupedScheduling.Fixture`)

Woven workers and weave subjects used by unit tests (not a product demo). Keep sample workers demo-oriented; keep fixture workers assertion-oriented.

## Tests (`GroupedScheduling.Tests`)

| Folder | Focus |
|---|---|
| `Engine/` | `ActionsHandler`, lane, nest, shutdown |
| `Weave/` | In-process Cecil weave + MSBuild weave |
| `Analyzers/` | TSCH001–005 |
| `Workers/` | Behavior through Fixture woven types |
| `Hosting/` | DI / hosted service (process-wide shutdown covered heavily by sample smoke) |

**Note:** Avoid `HandlerRegistry.ShutdownAll*` in parallel unit tests—it poisons other tests sharing the process registry.
