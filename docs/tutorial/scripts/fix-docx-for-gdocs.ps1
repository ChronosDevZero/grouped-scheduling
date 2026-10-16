#Requires -Version 5.1
<#
.SYNOPSIS
  Rewrites Pandoc DrawingML so Google Docs keeps embedded PNG images.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DocxPath
)

$ErrorActionPreference = 'Stop'
$DocxPath = (Resolve-Path $DocxPath).Path
$tmp = Join-Path $env:TEMP ("gs-gdocs-fix-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null

function Escape-Xml([string]$s) {
    if ($null -eq $s) { return '' }
    return ($s -replace '&', '&amp;' -replace '"', '&quot;' -replace '<', '&lt;' -replace '>', '&gt;')
}

try {
    $zipPath = Join-Path $tmp 'book.zip'
    Copy-Item $DocxPath $zipPath
    Expand-Archive -Path $zipPath -DestinationPath (Join-Path $tmp 'unpacked') -Force

    $docPath = Join-Path $tmp 'unpacked\word\document.xml'
    $xml = [IO.File]::ReadAllText($docPath)

    $count = 0
    $pattern = [regex]'(?s)<w:drawing>.*?</w:drawing>'

    $xml = $pattern.Replace($xml, {
        param($m)
        $block = $m.Value
        if ($block -notmatch 'r:embed="([^"]+)"') { return $block }
        $rid = $Matches[1]

        $cx = '5486400'
        $cy = '3086550'
        if ($block -match '<wp:extent[^>]*cx="(\d+)"[^>]*cy="(\d+)"') {
            $cx = $Matches[1]
            $cy = $Matches[2]
        }

        $name = 'Picture'
        if ($block -match 'wp:docPr[^>]*descr="([^"]*)"') {
            if ($Matches[1]) { $name = $Matches[1] }
        }
        $name = Escape-Xml $name

        $script:count = $script:count + 1
        $id = $script:count

        return "<w:drawing><wp:inline distT=`"0`" distB=`"0`" distL=`"0`" distR=`"0`"><wp:extent cx=`"$cx`" cy=`"$cy`"/><wp:docPr id=`"$id`" name=`"Picture $id`" descr=`"$name`"/><wp:cNvGraphicFramePr><a:graphicFrameLocks xmlns:a=`"http://schemas.openxmlformats.org/drawingml/2006/main`" noChangeAspect=`"1`"/></wp:cNvGraphicFramePr><a:graphic xmlns:a=`"http://schemas.openxmlformats.org/drawingml/2006/main`"><a:graphicData uri=`"http://schemas.openxmlformats.org/drawingml/2006/picture`"><pic:pic xmlns:pic=`"http://schemas.openxmlformats.org/drawingml/2006/picture`"><pic:nvPicPr><pic:cNvPr id=`"$id`" name=`"Picture $id`" descr=`"$name`"/><pic:cNvPicPr><a:picLocks noChangeAspect=`"1`"/></pic:cNvPicPr></pic:nvPicPr><pic:blipFill><a:blip r:embed=`"$rid`"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=`"0`" y=`"0`"/><a:ext cx=`"$cx`" cy=`"$cy`"/></a:xfrm><a:prstGeom prst=`"rect`"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing>"
    })

    $xml = $xml.Replace('w:val="CaptionedFigure"', 'w:val="Normal"')
    $xml = $xml.Replace('w:val="ImageCaption"', 'w:val="Normal"')

    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [IO.File]::WriteAllText($docPath, $xml, $utf8NoBom)

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $DocxPath) { Remove-Item $DocxPath -Force }

    # Zip entry names must use forward slashes for Office packages.
    $unpacked = Join-Path $tmp 'unpacked'
    $zip = [System.IO.Compression.ZipFile]::Open($DocxPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -Path $unpacked -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($unpacked.Length + 1).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $_.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $zip.Dispose()
    }

    Write-Host "Patched $count drawing(s) for Google Docs: $DocxPath"
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
