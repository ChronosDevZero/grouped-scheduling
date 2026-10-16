# Schedule modes

Each `[ScheduleGroup(name, mode)]` assigns the method to a **named group** with a concurrency policy. Groups are scoped to one `ActionsHandler` (one instance field, or one static field per declaring type)—same name on different classes does **not** share a queue.

| Mode | Exclusive lane | Inside the active group |
|---|---|---|
| `ExclusiveSerial` | One exclusive group active at a time; waiters FIFO | One task at a time |
| `ExclusiveParallel` | One exclusive group active at a time; waiters FIFO | Tasks may overlap |
| `FullyConcurrent` | **Bypasses** the exclusive lane | Runs immediately alongside other work |

## Rules of thumb

- Use **ExclusiveSerial** when methods in a group must not overlap (e.g. mutating shared state).
- Use **ExclusiveParallel** when the *group* must exclude other exclusive groups, but work inside may run together.
- Use **FullyConcurrent** for fire-and-forget-friendly work that must not wait on exclusive groups (still subject to nest rules).

## Same group name, different modes

On one type, all methods that share a group **name** must use the **same** `ScheduleMode`. Conflicting modes fail the weave (`InvalidOperationException` → build failure).

## Instance vs static

- Instance attributed methods → one handler per **object**.
- Static attributed methods → one handler per **declaring type** (closed generic types get their own static state).
- Instance and static on the same type are **independent**.
