# GroupedScheduling docs

Attribute-driven, group-aware async scheduling with **build-time IL weaving**. You write normal `async` methods; callers keep `await`, delegates, and reflection. The weaver injects scheduling, nesting checks, and linked cancellation.

| Doc | Audience |
|---|---|
| [Getting started](getting-started.md) | App authors |
| [Schedule modes](schedule-modes.md) | App authors |
| [Nesting](nesting.md) | App authors |
| [Shutdown & tokens](shutdown.md) | App authors / hosts |
| [Method signatures](signatures.md) | App authors |
| [Diagnostics](diagnostics.md) | IDE / build errors |
| [Architecture](architecture.md) | Maintainers |
| [Weaving](weaving.md) | Maintainers |
| [Packaging](packaging.md) | Consumers / CI |
| [Samples & tests](samples-and-tests.md) | Contributors |
| [Limitations](limitations.md) | Everyone |
| [**Deep dive tutorial** (book)](tutorial/README.md) | Learners / maintainers — line-level; builds a Google Docs–friendly `.docx` |

**Package:** `GroupedScheduling.Build` (runtime + analyzers + MSBuild weaver).  
**TFM:** `net11.0` (analyzers: `netstandard2.0`).  
**Sample:** [`samples/GroupedScheduling.Sample`](../samples/GroupedScheduling.Sample).
