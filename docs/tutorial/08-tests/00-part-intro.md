# Part VII — Tests as executable specs

**Projects:** `GroupedScheduling.Tests`, `GroupedScheduling.Fixture`

```bash
dotnet test GroupedScheduling.Tests/GroupedScheduling.Tests.csproj
```

## Folder map

| Folder | Focus | Book part |
|---|---|---|
| `Engine/` | Handler, lane, nest, shutdown, coverage | II / II-B |
| `Weave/` | In-process Cecil + MSBuild weave | III |
| `Analyzers/` | TSCH001–005 | IV |
| `Workers/` | Behavior through Fixture woven types | integration |
| `Hosting/` | DI / hosted service | V |

## How to use tests while learning

1. Pick a rule from a deep-dive (“serial does not overlap”).
2. Find the test that would fail if you broke it.
3. Change the production code on purpose in a scratch branch — watch the test name light up red.
4. Revert.

That loop beats re-reading prose alone.

## Critical warning

**Do not** call `HandlerRegistry.ShutdownAll*` from parallel unit tests that share the process — it cancels other tests’ handlers. Prefer disposing the specific owner, or isolate with non-parallel collections if you must call process-wide shutdown. Sample `HostStopSmoke` is the intentional process-stop demo.
