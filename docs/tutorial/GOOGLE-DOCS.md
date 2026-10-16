# Making the book look good in Google Docs

The build targets a clean import: Heading 1–3, TOC, plain code, simple tables, and PNGs rewritten into Google-friendly DrawingML (`fix-docx-for-gdocs.ps1`).

## Upload (important for images)

1. Open [Google Drive](https://drive.google.com).
2. Upload **`GroupedScheduling-DeepDive.docx`** (the one from `docs/tutorial/` after a fresh build).
3. Wait until Drive finishes processing (thumbnail/preview appears).
4. Right-click → **Open with → Google Docs**.

If diagrams are still missing:

1. Confirm you used a DOCX built **after** the Google Docs image fix (file size should be roughly **~350–400 KB**, not ~80 KB).
2. Rebuild:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File docs/tutorial/scripts/build-docx.ps1
```

3. Upload again as a **new** file (don’t reuse a broken Docs copy — conversion happens once at import).
4. Fallback A: upload `GroupedScheduling-DeepDive.html` (self-contained, images embedded) to Drive → **Open with → Google Docs**.
5. Fallback B: open the DOCX in **Microsoft Word** / Word Online, then save/copy into Docs.
6. Last resort: insert from `docs/tutorial/assets/*.png` manually at the figure captions.

## First-minute polish (optional but nice)

1. **Show the outline (current Docs UI):**
   - Top-left: open **Show tabs & outlines**.
   - On the document tab, open **Tab options** (⋮) → **Show outline**.
   - Shortcut: hold **Ctrl+Alt**, then press **A**, then **H** (Mac: **Ctrl+⌘**, then **A**, then **H**).
   - Chapter titles from Heading 1–3 appear there (Google removed **View → Show document outline**).
2. Click the TOC near the top → **Refresh** if needed (**Insert → Table of contents** if missing).
3. **File → Page setup** — Letter or A4; 0.75–1" margins read well.
4. Optional theme pass:
   - Select all → set body font to **Arial** or **Roboto** (Docs defaults travel well).
   - Keep **Heading 1/2/3** styles (do not manually bold titles) so the outline stays linked.
5. Code blocks import as monospace paragraphs. If a block looks cramped: select it → increase paragraph spacing slightly (before/after 4–6 pt).

## What to avoid in the Markdown source

These survive poorly into Docs:

- HTML / custom CSS
- Mermaid or other render-only diagrams (use the PNG files under `assets/`)
- Nested / merged tables
- Very deep heading levels (`####`+) — we stop at `###`

## Rebuilding after edits

Edit Markdown → run `scripts/build-docx.ps1` → upload a **new** Drive file (or replace the `.docx`, then open a fresh Docs copy). Personal highlights in an old Docs copy are not merged back into Markdown.
