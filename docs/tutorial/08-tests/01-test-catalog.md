# Test catalog (where truth is asserted)

## Engine

| Area | Typical files | Asserts |
|---|---|---|
| Handler / schedule | `ActionsHandlerTests.cs` | Schedule, shutdown reject, in-flight |
| Nest | `NestingTests.cs`, `NestCancelCoverageTests.cs` | Same handler throw; cancel interactions |
| Shutdown | `ShutdownTests.cs` | Drain, tokens, post-shutdown |
| Coverage | `EngineCoverageTests.cs` | Edge paths / branches |

## Weave

| File | Asserts |
|---|---|
| `WeaveTests.cs` | In-process Cecil rewrite behaviors |
| `WeaveCoverageTests.cs` | Broader weave matrix |
| `MsBuildWeaveTests.cs` | Real MSBuild target path |

Fixture subjects (`WeaveSubjects/`, `Workers/`) are the raw material being woven.

## Analyzers

`AnalyzerTests.cs` — each TSCH id has cases: should report / should not report.

## Workers / Hosting

| Folder | Role |
|---|---|
| `Workers/WorkerBehaviorTests.cs` | End-to-end through woven fixture workers |
| `Hosting/HostingTests.cs` | `AddGroupedScheduling` registration / stop hook |

## Learning recipe

When a tutorial chapter says **See also**, open that test file and read the test names first — they are often better titles than the prose headings.
