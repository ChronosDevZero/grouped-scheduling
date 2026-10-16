#Requires -Version 5.1
<#
.SYNOPSIS
  Builds GroupedScheduling-DeepDive.docx for Word / Google Docs upload.

.DESCRIPTION
  Concatenates tutorial Markdown in a stable order and runs Pandoc with
  Google Docs–friendly settings:
    - Heading styles (outline sidebar in Docs)
    - TOC (depth 3)
    - Reference DOCX for predictable Normal/Heading fonts
    - No raw HTML passthrough surprises

.NOTES
  Install Pandoc: https://pandoc.org/installing.html
  winget install --id JohnMacFarlane.Pandoc -e
#>
[CmdletBinding()]
param(
    [string]$OutFile = "",
    [switch]$SkipToc
)

$ErrorActionPreference = 'Stop'

$tutorialRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$repoHint = Split-Path (Split-Path $tutorialRoot -Parent) -Parent
if (-not $OutFile) {
    $OutFile = Join-Path $tutorialRoot 'GroupedScheduling-DeepDive.docx'
}

function Find-Pandoc {
    $cmd = Get-Command pandoc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidate = Join-Path $env:LOCALAPPDATA 'Pandoc\pandoc.exe'
    if (Test-Path $candidate) { return $candidate }
    throw "Pandoc not found. Install it, then re-run. (winget install --id JohnMacFarlane.Pandoc -e)"
}

$pandoc = Find-Pandoc
$referenceDoc = Join-Path $PSScriptRoot 'gdocs-reference.docx'
if (-not (Test-Path $referenceDoc)) {
    & $pandoc -o $referenceDoc --print-default-data-file reference.docx
}

# Explicit chapter order — do not rely on filesystem enumeration alone.
$relativeOrder = @(
    '00-title.md'
    '01-orientation\00-preface.md'
    '01-orientation\01-how-to-read.md'
    '01-orientation\02-mental-model.md'
    '01-orientation\03-figures.md'
    '02-public-api\00-part-intro.md'
    '02-public-api\01-schedule-group-attribute.md'
    '02-public-api\02-schedule-mode.md'
    '02-public-api\03-event-action-group.md'
    '02-public-api\04-actions-handler.md'
    '02-public-api\05-group-lane-scheduler.md'
    '03-runtime-engine\00-part-intro.md'
    '03-runtime-engine\01-scheduling-nest.md'
    '03-runtime-engine\02-scheduling-runtime.md'
    '03-runtime-engine\03-scheduling-owner-state.md'
    '03-runtime-engine\04-instance-and-static-state.md'
    '03-runtime-engine\05-handler-registry.md'
    '03-runtime-engine\06-scheduling-weave-hooks.md'
    '04-weaving\00-part-intro.md'
    '04-weaving\01-why-body-wrapping.md'
    '04-weaving\02-weave-options-result-diagnostics.md'
    '04-weaving\03-module-weaver-discovery.md'
    '04-weaving\04-rewrite-method.md'
    '04-weaving\05-dispose-injection.md'
    '04-weaving\06-assembly-io.md'
    '05-analyzers\00-part-intro.md'
    '05-analyzers\01-diagnostics-catalog.md'
    '05-analyzers\02-schedule-group-facts.md'
    '05-analyzers\03-analyzers-tsch.md'
    '05-analyzers\04-unsupported-parameter-messages.md'
    '06-hosting-and-build\00-part-intro.md'
    '06-hosting-and-build\01-add-grouped-scheduling.md'
    '06-hosting-and-build\02-hosted-service.md'
    '06-hosting-and-build\03-weave-assembly-task.md'
    '06-hosting-and-build\04-packaging.md'
    '07-samples\00-part-intro.md'
    '07-samples\01-smoke-catalog.md'
    '08-tests\00-part-intro.md'
    '08-tests\01-test-catalog.md'
    '09-appendix\00-glossary.md'
)

$inputs = foreach ($rel in $relativeOrder) {
    $full = Join-Path $tutorialRoot $rel
    if (-not (Test-Path $full)) {
        throw "Missing chapter file: $rel"
    }
    $full
}

# Include chapter folders so markdown image paths like ../assets/*.png resolve
# the same way they do when previewing a chapter file in-editor.
$resourceDirs = @(
    $tutorialRoot.Path
    (Join-Path $tutorialRoot.Path 'assets')
) + @(
    Get-ChildItem -Path $tutorialRoot.Path -Directory |
        Where-Object { $_.Name -match '^\d{2}-' } |
        ForEach-Object { $_.FullName }
)
$resourcePath = ($resourceDirs -join [IO.Path]::PathSeparator)

$pandocArgs = @(
    '-f', 'markdown+gfm_auto_identifiers+pipe_tables+fenced_code_attributes'
    '-t', 'docx'
    '-o', $OutFile
    '--resource-path', $resourcePath
    '--reference-doc', $referenceDoc
    '--syntax-highlighting=none'   # plain monospace travels better into Google Docs
    '-M', 'lang=en-US'
)

if (-not $SkipToc) {
    $pandocArgs += @('--toc', '--toc-depth=3')
}

$pandocArgs += $inputs

Write-Host "Pandoc: $pandoc"
Write-Host "Chapters: $($inputs.Count)"
Write-Host "Output: $OutFile"

& $pandoc @pandocArgs
if ($LASTEXITCODE -ne 0) {
    throw "Pandoc failed with exit code $LASTEXITCODE"
}

# Pandoc DrawingML is often stripped by Google Docs' importer — rewrite shapes.
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'fix-docx-for-gdocs.ps1') -DocxPath $OutFile
if ($LASTEXITCODE -ne 0) {
    throw "Google Docs DOCX fix failed with exit code $LASTEXITCODE"
}

$item = Get-Item $OutFile
Write-Host ("Built {0} ({1:N1} KB)" -f $item.Name, ($item.Length / 1KB))

# Self-contained HTML fallback — Drive sometimes imports images from HTML more reliably.
$htmlOut = Join-Path $tutorialRoot 'GroupedScheduling-DeepDive.html'
$htmlArgs = @(
    '-f', 'markdown+gfm_auto_identifiers+pipe_tables+fenced_code_attributes'
    '-t', 'html5'
    '-o', $htmlOut
    '--resource-path', $resourcePath
    '--embed-resources'
    '--standalone'
    '-M', 'lang=en-US'
    '--toc'
    '--toc-depth=3'
) + $inputs
& $pandoc @htmlArgs
if ($LASTEXITCODE -eq 0) {
    Write-Host ("Also built {0} ({1:N1} KB) as Google Docs fallback" -f (Split-Path $htmlOut -Leaf), ((Get-Item $htmlOut).Length / 1KB))
}

Write-Host ""
Write-Host "Google Docs: upload the .docx to Drive, wait for preview, then Open with Google Docs."
Write-Host "If PNGs are missing, try uploading GroupedScheduling-DeepDive.html instead."
Write-Host "Outline: Show tabs and outlines -> tab menu Show outline (or Ctrl+Alt, A, H)."
Write-Host "Repo hint: $repoHint"
