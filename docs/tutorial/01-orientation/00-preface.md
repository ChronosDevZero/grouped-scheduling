# Preface

This book teaches **GroupedScheduling** the way a patient senior engineer would: not only *what* each type does, but *why each statement exists*, which concurrency hazard it prevents, and which test would fail if you deleted it.

The shorter docs under `docs/` are for **app authors**. This tutorial is for **people who want to learn the system deeply** — maintainers, curious contributors, or anyone studying async scheduling, IL weaving, and Roslyn analyzers in one coherent codebase.

## What you will be able to do

- Explain the end-to-end path from `[ScheduleGroup]` to a running lane grant.
- Read `ActionsHandler` / `GroupLaneScheduler` without guessing.
- Predict nest failures, shutdown drains, and mode conflicts before they happen.
- Navigate the weaver and analyzers with a mental map.

## What this book is not

- Not a substitute for the user-facing guides (getting started, modes, shutdown).
- Not a Mono.Cecil or Roslyn textbook — we teach those concepts *as they appear* in this repo.
- Not auto-generated “dump every token” commentary. Blank lines and braces are skipped; **every behavioral statement** is explained.

## Conventions

| Marker | Meaning |
|---|---|
| **Concept** | Idea you need before the next code block |
| **Why it matters** | Product / correctness reason |
| **Line walkthrough** | Statement-level explanation of real source |
| **What breaks if…** | Failure mode if you “simplify” the code |
| **See also** | Pointer to tests, samples, or short docs |

Code listings mirror the repository. Line numbers refer to the file at the time of writing; if the file moves, search by type/method name.

## How to use Google Docs

Upload the generated `.docx`, open with Google Docs, and show the outline: **Show tabs & outlines** (top-left) → tab **⋮** → **Show outline** (shortcut: **Ctrl+Alt**, then **A**, then **H**). Each chapter title (`#`) is a top-level outline entry. Jump around freely — chapters are written to stand alone after you have read the mental model once.
