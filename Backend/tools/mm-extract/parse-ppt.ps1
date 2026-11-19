# Parses the media-monitoring PPTX into structured JSON (topics + sentiment impression lists per city).
param(
    [string]$Pptx = "C:\Projects\تحليل الرصد الإعلامي.pptx",
    [string]$OutJson = "$PSScriptRoot\mm-data.json"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$work = Join-Path $env:TEMP 'pptx_mm_parse'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
[System.IO.Compression.ZipFile]::ExtractToDirectory($Pptx, $work)

$ns = 'http://schemas.openxmlformats.org/drawingml/2006/main'

function Get-Paragraphs([int]$slide) {
    $f = Join-Path $work "ppt\slides\slide$slide.xml"
    $xml = [xml](Get-Content $f -Raw -Encoding UTF8)
    $m = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $m.AddNamespace('a', $ns)
    $result = @()
    foreach ($p in $xml.SelectNodes('//a:p', $m)) {
        $runs = @($p.SelectNodes('.//a:t', $m) | ForEach-Object { $_.InnerText })
        $line = ($runs -join '').Trim()
        if ($line.Length -gt 0) { $result += $line }
    }
    return $result
}

# Remove tatweel (U+0640) and whitespace for header/keyword matching.
function Norm([string]$s) {
    return ($s -replace [char]0x0640, '') -replace '\s', ''
}

function Clean([string]$s) {
    # Remove decorative tatweel (kashida), collapse whitespace, trim trailing dot/space.
    $t = ($s -replace [char]0x0640, '') -replace '\s+', ' '
    $t = $t.Trim()
    return $t.TrimEnd('.', [char]0x066B, ' ').Trim()
}

function Is-Header([string]$line) {
    $n = Norm $line
    return ($n.Length -le 25 -and $line -notmatch '[.]')
}

$pairs = @(
    @{ id = 'riyadh';  summary = 6;  sentiment = 7 }
    @{ id = 'jeddah';  summary = 8;  sentiment = 9 }
    @{ id = 'makkah';  summary = 10; sentiment = 11 }
    @{ id = 'dammam';  summary = 12; sentiment = 13 }
    @{ id = 'hail';    summary = 14; sentiment = 15 }
    @{ id = 'najran';  summary = 16; sentiment = 17 }
    @{ id = 'madinah'; summary = 18; sentiment = 19 }
    @{ id = 'tabuk';   summary = 20; sentiment = 21 }
    @{ id = 'sakaka';  summary = 22; sentiment = 23 }
)

$cities = [ordered]@{}

foreach ($pair in $pairs) {
    # ---- Topics from summary slide (between الموضوعات header and التحديات header) ----
    $topics = @()
    $inTopics = $false
    foreach ($line in (Get-Paragraphs $pair.summary)) {
        $n = Norm $line
        if ($n -match 'الموضوعات') { $inTopics = $true; continue }
        if ($n -match 'التحديات') { $inTopics = $false; continue }
        if ($inTopics) {
            if ($n -match 'الملخص') { continue }
            $topics += (Clean $line)
        }
    }

    # ---- Impression lists from sentiment slide ----
    $neutral = @(); $positive = @(); $negative = @()
    $section = $null
    foreach ($line in (Get-Paragraphs $pair.sentiment)) {
        $n = Norm $line
        if ($line -match '^\d+$') { continue }                 # slide number
        if ($n -match 'تحليلالانطباعات') { continue }          # slide title
        if (Is-Header $line) {
            if ($n -match 'حايد') { $section = 'neutral'; continue }
            elseif ($n -match 'يجاب') { $section = 'positive'; continue }
            elseif ($n -match 'سلب') { $section = 'negative'; continue }
        }
        if ($null -eq $section) { continue }
        $c = Clean $line
        switch ($section) {
            'neutral'  { if ($neutral  -notcontains $c) { $neutral  += $c } }
            'positive' { if ($positive -notcontains $c) { $positive += $c } }
            'negative' { if ($negative -notcontains $c) { $negative += $c } }
        }
    }

    # Makkah's sentiment slide (slide 11) begins with a copy-paste of Jeddah's block
    # before the Makkah-specific content. Drop the leading Jeddah block.
    if ($pair.id -eq 'makkah') {
        if ($neutral.Count -ge 2)  { $neutral  = @($neutral[2..($neutral.Count - 1)]) }
        if ($negative.Count -ge 2) { $negative = @($negative[2..($negative.Count - 1)]) }
        $positive = @()
    }

    # Some slides duplicate neutral bullets into the negative/positive columns; de-dupe across sections.
    $negative = @($negative | Where-Object { $neutral -notcontains $_ })
    $positive = @($positive | Where-Object { $neutral -notcontains $_ -and $negative -notcontains $_ })

    $cities[$pair.id] = [ordered]@{
        topics   = @($topics | ForEach-Object { $_ })
        neutral  = @($neutral)
        positive = @($positive)
        negative = @($negative)
    }
}

$json = $cities | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($OutJson, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output "Wrote $OutJson"
foreach ($k in $cities.Keys) {
    $c = $cities[$k]
    Write-Output ("{0,-9} topics={1} neu={2} pos={3} neg={4}" -f $k, $c.topics.Count, $c.neutral.Count, $c.positive.Count, $c.negative.Count)
}
