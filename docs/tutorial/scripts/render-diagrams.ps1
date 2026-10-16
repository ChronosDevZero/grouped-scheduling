#Requires -Version 5.1
<#
.SYNOPSIS
  Renders docs/tutorial/assets/*.svg to matching *.png via Chrome/Edge headless.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$assets = Resolve-Path (Join-Path $PSScriptRoot '..\assets')

$chrome = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $chrome) {
    throw "Chrome or Edge not found. Install one to render SVG -> PNG."
}

$svgs = Get-ChildItem -Path $assets -Filter '*.svg' | Sort-Object Name
if ($svgs.Count -eq 0) {
    throw "No SVG files in $assets"
}

foreach ($svg in $svgs) {
    $png = Join-Path $assets.Path ($svg.BaseName + '.png')
    $uri = ([Uri]$svg.FullName).AbsoluteUri

    $raw = Get-Content -Raw -Encoding UTF8 $svg.FullName
    $w = 1280
    $h = 720
    if ($raw -match 'width="(\d+)"') { $w = [int]$Matches[1] }
    if ($raw -match 'height="(\d+)"') { $h = [int]$Matches[1] }

    Write-Host "Rendering $($svg.Name) -> $($svg.BaseName).png ($w x $h)"

    $argList = @(
        '--headless=new'
        '--disable-gpu'
        '--hide-scrollbars'
        "--window-size=$w,$h"
        "--screenshot=$png"
        $uri
    )

    $p = Start-Process -FilePath $chrome -ArgumentList $argList -Wait -PassThru -NoNewWindow
    Start-Sleep -Milliseconds 200

    if (-not (Test-Path $png) -or (Get-Item $png).Length -lt 1000) {
        throw "Failed to create $png (chrome exit $($p.ExitCode))"
    }

    $len = (Get-Item $png).Length
    Write-Host ("  ok ({0:N1} KB)" -f ($len / 1KB))
}

Write-Host "Done. PNGs in $assets"
