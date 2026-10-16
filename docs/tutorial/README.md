# GroupedScheduling — Deep Dive Tutorial (book)

Coder-oriented, line-level walkthrough of the whole solution. Companion to the shorter user docs in [`docs/`](../README.md).

## How this is maintained

| Layer | Role |
|---|---|
| Markdown under `docs/tutorial/**` | Source of truth (git-friendly) |
| `scripts/build-docx.ps1` | Builds one Google Docs–friendly `.docx` |
| `GroupedScheduling-DeepDive.docx` / `.html` | Generated book (gitignored — rebuild before sharing) |

Do **not** hand-edit the `.docx` as the primary source — edit Markdown, then rebuild. Generated DOCX/HTML are **not** committed; SVG/PNG under `assets/` are.

## Build the DOCX

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File docs/tutorial/scripts/build-docx.ps1
```

Output: `docs/tutorial/GroupedScheduling-DeepDive.docx`

Requirements: [Pandoc](https://pandoc.org/) on `PATH` (or under `%LOCALAPPDATA%\Pandoc\`).

## Open nicely in Google Docs

See [GOOGLE-DOCS.md](GOOGLE-DOCS.md). Short version:

1. Upload the `.docx` to Google Drive.
2. **Open with → Google Docs**.
3. Refresh TOC; open the outline via **Show tabs & outlines** → tab **⋮** → **Show outline** (or **Ctrl+Alt**, then **A**, then **H**).

## Reading paths

| You are… | Start at |
|---|---|
| New to the library | Part I → mental model, then public API |
| Changing runtime behavior | Part II → handler + lane, then nest/registry |
| Changing IL / weave | Part III |
| Diagnosing TSCH\* / GSWEAVE\* | Part IV + [diagnostics](../diagnostics.md) |
| Learning by running | Parts VI–VII |

## Status

| Part | Status |
|---|---|
| I — Orientation | Done |
| II — Public API & lane engine | Done |
| II-B — Nest, owner state, registry, weave hooks | Done |
| III — Weaver | Done |
| IV — Analyzers | Done |
| V — Hosting & build / packaging | Done |
| VI — Samples | Done (catalog + how to study) |
| VII — Tests as specs | Done (catalog) |
| Appendix — Glossary | Done |

**Diagrams:** [`assets/`](assets/README.md) (SVG + PNG). Gallery chapter: `01-orientation/03-figures.md`.

Optional future polish: more IL opcode listings, per-smoke walkthrough pages.
