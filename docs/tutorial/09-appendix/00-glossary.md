# Appendix — Glossary

| Term | Meaning in this repo |
|---|---|
| **Group** | Named schedule bucket with a `ScheduleMode` on one handler |
| **Handler** | `ActionsHandler` — per instance or per static type |
| **Lane** | `GroupLaneScheduler` — exclusive FIFO activation |
| **Nest** | `AsyncLocal` stack of active handlers; same-handler re-entry forbidden |
| **Owner state** | `InstanceSchedulingState` / `StaticSchedulingState` |
| **Owner CTS** | Canceled on dispose/shutdown; linked into call tokens |
| **Weave** | Post-compile Mono.Cecil rewrite of attributed methods |
| **Stub** | Public method body after weave (calls hooks) |
| **`__Original`** | Private method holding the user’s original IL |
| **Hook** | `SchedulingWeaveHooks.Schedule*` entry point |
| **TSCH\*** | Roslyn diagnostic ids |
| **GSWEAVE\*** | Weave diagnostic ids |
| **GSBUILD\*** | MSBuild task diagnostic ids |
| **Fixture** | Test-only woven workers/subjects |
| **Smoke** | Sample console scenario proving one behavior |

## End-to-end checklist (you “get it” when…)

1. You can draw attribute → weave → hooks → handler → lane without notes.
2. You know why `"orders"` on two classes does not share a queue.
3. You can explain why nested same-instance calls throw.
4. You know why methods need `CancellationToken` for in-flight shutdown cancel.
5. You know why body wrapping beats call-site rewriting for delegates.

## Where to go next in the short docs

| Need | Doc |
|---|---|
| Ship an app | `docs/getting-started.md` |
| Pick a mode | `docs/schedule-modes.md` |
| Debug a diagnostic | `docs/diagnostics.md` |
| Understand limits | `docs/limitations.md` |
