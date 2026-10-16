# Tutorial diagram assets

| File | Topic |
|---|---|
| `01-end-to-end` | Attribute → weave → runtime → host stop |
| `02-runtime-call-graph` | Stub → hooks → handler → nest/lane |
| `03-exclusive-lane` | `GroupLaneScheduler` mental model |
| `04-nest-stack` | Allowed vs forbidden nesting |
| `05-shutdown-layers` | Host → registry → owner → handler tokens |
| `06-weave-before-after` | Method rewrite + injections |
| `07-solution-map` | Projects + NuGet layout |

- **Source of truth:** `.svg` (edit these)
- **Exported:** `.png` (embedded in Markdown / DOCX / Google Docs)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File docs/tutorial/scripts/render-diagrams.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File docs/tutorial/scripts/build-docx.ps1
```

Requires Chrome or Edge for headless SVG → PNG.
