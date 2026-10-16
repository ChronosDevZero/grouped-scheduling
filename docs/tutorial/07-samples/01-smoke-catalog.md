# Sample smoke catalog (what each proves)

| Smoke | Proves | Runtime chapter to re-read |
|---|---|---|
| `ArgAndNestedSmoke` | Business args captured in closures; nested types weave | Rewrite (closures) |
| `ValueTaskSmoke` | `ValueTask` / `ValueTask<T>` / tuple Task hooks | WeaveHooks Path D |
| `GenericsSmoke` | Declaring-type generics (`EntityStore<T>`) + generic methods | Rewrite generics / SelfClosedReference |
| `CombinedGenericsSmoke` | `Mapper<TIn>.MapAsync<TOut>` combined GPs | Closure GP append |
| `StaticOnlySmoke` | Static-only worker; type handler / cctor | Static state + registry |
| `NestRejectSmoke` | Same-instance nest throws (incl. reflection path) | SchedulingNest |
| `CrossInstanceNestSmoke` | A→B across instances OK | Nest allow table |
| `CancelInFlightSmoke` | Method token cancels in-flight work | Linked tokens / owner state |
| `ExceptionPropagateSmoke` | Exception reaches caller; exclusive lane frees | Handler try/finally Release |
| `ParallelOverlapSmoke` | ExclusiveParallel overlap | GroupLaneScheduler grants |
| `ModeRoundsSmoke` | Mode rounds + serial non-overlap | ScheduleMode + lane |
| `HostStopSmoke` | Mid-run stop + post-stop reject | Hosted service + ShutdownAsync |

## Workers worth opening

| Worker | Why |
|---|---|
| `ModesDemoWorker` | ExclusiveSerial / Parallel / FullyConcurrent side by side |
| `LifecycleDemoWorker` | Cancel + exception + nest scenarios |
| `GenericDemoWorkers` / `MapperDemo` | Generic weave stress |
| `ValueTaskDemoWorker` | ValueTask surface |
| `StaticOnlyJobs` | Static scheduling |
| `AuxWorker` | Second instance for cross calls |

## Fixture vs sample

`GroupedScheduling.Fixture` is for **tests** (assertion-oriented workers / weave subjects). Sample workers stay **demo-oriented**. Do not conflate them when hunting examples.
