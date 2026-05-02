# SPDX-License-Identifier: GPL-3.0-or-later
<#
.SYNOPSIS
    Compares D2R `data\local\lng\strings\*.json` files between a mod tree
    and a vanilla CASC extraction, reporting id/Key/enUS differences for
    any file that exists in both locations.

.DESCRIPTION
    The vanilla CASC strings layout is `<root>\data\local\lng\strings\*.json`,
    while a mod typically ships only the files it overrides. This script
    walks the intersection of the two directories and emits a single
    `string-compare.txt` report containing, per shared file, only the
    entries whose `id` is present in one side but not the other (the
    report shows `id` + `Key` for each such entry). Same-id entries
    whose `Key` or `enUS` text differ are intentionally not reported —
    only genuine missing ids matter for the keep-in-sync workflow.

    Files that exist in only one side are listed in a summary section and
    are otherwise skipped (per the issue spec: "only for files found in
    both locations").

.PARAMETER ModPath
    Path to the mod's `lng` folder (the parent of `strings\`) OR directly
    to the `strings\` folder. Both shapes are accepted.

.PARAMETER VanillaPath
    Same as -ModPath, for the vanilla CASC extraction.

.PARAMETER OutputPath
    Where to write the report. Defaults to `string-compare.txt` in the
    current working directory.

.PARAMETER MaxId
    Ids strictly greater than this value are ignored on both sides.
    Defaults to 40000 because the vanilla D2R CASC string tables stop
    well below that threshold; anything above it is mod-only by
    definition and would otherwise dominate the "mod-only ids" bucket
    with noise.

.EXAMPLE
    pwsh .\scripts\Compare-Strings.ps1 `
        -ModPath     'C:\z_GitHub\d2r-reimagined-mod\data\local\lng' `
        -VanillaPath 'C:\Users\lemin\Downloads\zz_D2R Tools\zz_Warlock Patch\CASC Files\data\local\lng\strings'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ModPath,

    [Parameter(Mandatory = $true)]
    [string]$VanillaPath,

    [string]$OutputPath = (Join-Path (Get-Location) 'string-compare.txt'),

    [int]$MaxId = 40000
)

$ErrorActionPreference = 'Stop'

function Resolve-StringsDir {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Path not found: $Path"
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $candidate = Join-Path $resolved 'strings'
    if (Test-Path -LiteralPath $candidate) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }
    return $resolved
}

function Read-StringsFile {
    param(
        [string]$FilePath,
        [int]$MaxId
    )

    # D2R strings files are UTF-8 (occasionally with BOM). Read as raw text
    # so PowerShell doesn't mangle the embedded `ÿcX` colour codes.
    $raw  = Get-Content -LiteralPath $FilePath -Raw -Encoding UTF8
    # `-Depth` is PowerShell 7+ only; the default limit in Windows PowerShell 5.1
    # is more than enough for a flat string-table array.
    $json = $raw | ConvertFrom-Json

    $byId = [System.Collections.Generic.Dictionary[int, pscustomobject]]::new()
    foreach ($entry in $json) {
        if ($null -eq $entry) { continue }
        if ($null -eq $entry.id) { continue }
        $id = [int]$entry.id
        # Mod-only id range: vanilla CASC tables top out well below 40000,
        # so anything above $MaxId would always show up as "mod-only" and
        # bury the genuinely interesting diffs.
        if ($id -gt $MaxId) { continue }
        # Last-write-wins; D2R files don't normally repeat ids but we don't
        # want to crash if they do.
        $byId[$id] = $entry
    }
    return $byId
}

function Format-Entry {
    param([pscustomobject]$Entry)

    if ($null -eq $Entry) { return '<missing>' }
    $key = if ($null -ne $Entry.Key) { [string]$Entry.Key } else { '' }
    return ("Key='{0}'" -f $key)
}

$modDir     = Resolve-StringsDir -Path $ModPath
$vanillaDir = Resolve-StringsDir -Path $VanillaPath

Write-Host "Mod strings dir    : $modDir"
Write-Host "Vanilla strings dir: $vanillaDir"

$modFiles     = Get-ChildItem -LiteralPath $modDir     -Filter '*.json' -File
$vanillaFiles = Get-ChildItem -LiteralPath $vanillaDir -Filter '*.json' -File

$modNames     = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]($modFiles     | ForEach-Object { $_.Name }),
    [System.StringComparer]::OrdinalIgnoreCase)
$vanillaNames = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]($vanillaFiles | ForEach-Object { $_.Name }),
    [System.StringComparer]::OrdinalIgnoreCase)

$shared    = $modNames     | Where-Object { $vanillaNames.Contains($_) } | Sort-Object
$onlyMod   = $modNames     | Where-Object { -not $vanillaNames.Contains($_) } | Sort-Object
$onlyVan   = $vanillaNames | Where-Object { -not $modNames.Contains($_) }     | Sort-Object

$report = [System.Text.StringBuilder]::new()
[void]$report.AppendLine('# D2R strings comparison report')
[void]$report.AppendLine(("Generated : {0:u}" -f (Get-Date)))
[void]$report.AppendLine("Mod       : $modDir")
[void]$report.AppendLine("Vanilla   : $vanillaDir")
[void]$report.AppendLine(("Max id    : {0} (ids > {0} are skipped as mod-only)" -f $MaxId))
[void]$report.AppendLine('')
[void]$report.AppendLine('## File coverage')
[void]$report.AppendLine(("Shared files          : {0}" -f @($shared).Count))
[void]$report.AppendLine(("Mod-only files        : {0}" -f @($onlyMod).Count))
[void]$report.AppendLine(("Vanilla-only files    : {0}" -f @($onlyVan).Count))
if (@($onlyMod).Count -gt 0) {
    [void]$report.AppendLine('')
    [void]$report.AppendLine('### Mod-only files (skipped)')
    foreach ($n in $onlyMod) { [void]$report.AppendLine("  - $n") }
}
if (@($onlyVan).Count -gt 0) {
    [void]$report.AppendLine('')
    [void]$report.AppendLine('### Vanilla-only files (skipped)')
    foreach ($n in $onlyVan) { [void]$report.AppendLine("  - $n") }
}
[void]$report.AppendLine('')

$totalOnlyMod = 0
$totalOnlyVan = 0

foreach ($name in $shared) {
    $modFile     = Join-Path $modDir     $name
    $vanillaFile = Join-Path $vanillaDir $name

    Write-Host "Comparing $name ..."
    $modById     = Read-StringsFile -FilePath $modFile     -MaxId $MaxId
    $vanillaById = Read-StringsFile -FilePath $vanillaFile -MaxId $MaxId

    $allIds = [System.Collections.Generic.SortedSet[int]]::new()
    foreach ($k in $modById.Keys)     { [void]$allIds.Add($k) }
    foreach ($k in $vanillaById.Keys) { [void]$allIds.Add($k) }

    $missingInVanilla = New-Object System.Collections.Generic.List[int]
    $missingInMod     = New-Object System.Collections.Generic.List[int]

    foreach ($id in $allIds) {
        $hasMod = $modById.ContainsKey($id)
        $hasVan = $vanillaById.ContainsKey($id)

        if ($hasMod -and -not $hasVan) {
            $missingInVanilla.Add($id) | Out-Null
            continue
        }
        if ($hasVan -and -not $hasMod) {
            $missingInMod.Add($id) | Out-Null
            continue
        }

        # Same-id entries (matching, key-changed, or enUS-changed) are
        # intentionally not reported — only missing ids matter.
    }

    $totalOnlyMod += $missingInVanilla.Count
    $totalOnlyVan += $missingInMod.Count

    [void]$report.AppendLine('================================================================')
    [void]$report.AppendLine("## $name")
    [void]$report.AppendLine(("Mod entries     : {0}" -f $modById.Count))
    [void]$report.AppendLine(("Vanilla entries : {0}" -f $vanillaById.Count))
    [void]$report.AppendLine(("Mod-only ids    : {0}" -f $missingInVanilla.Count))
    [void]$report.AppendLine(("Vanilla-only ids: {0}" -f $missingInMod.Count))
    [void]$report.AppendLine('')

    if ($missingInVanilla.Count -gt 0) {
        [void]$report.AppendLine('### Ids in mod but not in vanilla')
        foreach ($id in $missingInVanilla) {
            [void]$report.AppendLine(("  id={0,-6} {1}" -f $id, (Format-Entry $modById[$id])))
        }
        [void]$report.AppendLine('')
    }
    if ($missingInMod.Count -gt 0) {
        [void]$report.AppendLine('### Ids in vanilla but not in mod')
        foreach ($id in $missingInMod) {
            [void]$report.AppendLine(("  id={0,-6} {1}" -f $id, (Format-Entry $vanillaById[$id])))
        }
        [void]$report.AppendLine('')
    }
}

[void]$report.AppendLine('================================================================')
[void]$report.AppendLine('## Totals across shared files')
[void]$report.AppendLine(("Mod-only ids    : {0}" -f $totalOnlyMod))
[void]$report.AppendLine(("Vanilla-only ids: {0}" -f $totalOnlyVan))

$outDir = Split-Path -Parent $OutputPath
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# UTF-8 without BOM, to match the input files.
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($OutputPath, $report.ToString(), $utf8NoBom)

Write-Host ''
Write-Host "Report written to: $OutputPath"
Write-Host ("Totals  -> mod-only={0}  vanilla-only={1}" -f `
    $totalOnlyMod, $totalOnlyVan)
