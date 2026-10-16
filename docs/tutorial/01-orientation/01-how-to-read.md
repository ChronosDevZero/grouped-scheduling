# How to read this tutorial

## Recommended first pass (about one evening)

1. This chapter (orientation).
2. Mental model (next chapter).
3. `ScheduleGroupAttribute` + `ScheduleMode` + `EventActionGroup`.
4. `ActionsHandler` then `GroupLaneScheduler`.

Stop there. Run the sample. Come back for nest, registry, weaver, and analyzers when you need them.

## Second pass (when you change code)

Open the chapter for the file you are editing. Read **What breaks if…** before you refactor. Then run the tests named in **See also**.

## Depth levels inside each file chapter

Most file chapters use this shape:

1. **Why this file exists** — one job statement.
2. **Concepts you need first** — async, CTS, locks, etc.
3. **Who calls whom** — ASCII call graph.
4. **Line walkthrough** — behavioral statements in order.
5. **What breaks if…** — deletion / “simplification” hazards.
6. **See also** — tests and samples.

## Google Docs formatting tips while studying

- Keep the outline open: **Show tabs & outlines** → tab **⋮** → **Show outline** (or **Ctrl+Alt**, then **A**, then **H**).
- Highlight in Docs freely — your highlights do not affect the Markdown source.
- When the book is regenerated, re-upload or replace the file; keep personal notes in a separate Docs file if you need them long-term.

## Companion short docs

| Topic | Short doc |
|---|---|
| Install / attribute / host | `docs/getting-started.md` |
| Modes | `docs/schedule-modes.md` |
| Nesting | `docs/nesting.md` |
| Shutdown | `docs/shutdown.md` |
| Architecture overview | `docs/architecture.md` |
