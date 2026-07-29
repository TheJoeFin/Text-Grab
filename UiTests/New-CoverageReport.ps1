[CmdletBinding()]
param(
    [string]$CoveragePath = (Join-Path $PSScriptRoot 'coverage.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot 'artifacts\coverage-report.md')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Validate-Coverage.ps1') -Path $CoveragePath

$inventory = Get-Content -LiteralPath $CoveragePath -Raw | ConvertFrom-Json
$items = @($inventory.items)
$lines = [System.Collections.Generic.List[string]]::new()
[void]$lines.Add('# UI automation coverage')
[void]$lines.Add('')
[void]$lines.Add("Generated: $([DateTimeOffset]::UtcNow.ToString('O'))")
[void]$lines.Add('')
[void]$lines.Add('## Status')
[void]$lines.Add('')
[void]$lines.Add('| Status | Checks |')
[void]$lines.Add('| --- | ---: |')
foreach ($group in $items | Group-Object implementationStatus | Sort-Object Name) {
    [void]$lines.Add("| $($group.Name) | $($group.Count) |")
}
[void]$lines.Add('')
[void]$lines.Add('## Coverage level')
[void]$lines.Add('')
[void]$lines.Add('| Level | Checks |')
[void]$lines.Add('| --- | ---: |')
foreach ($group in $items | Group-Object coverageLevel | Sort-Object Name) {
    [void]$lines.Add("| $($group.Name) | $($group.Count) |")
}
[void]$lines.Add('')
[void]$lines.Add('## Inventory')
[void]$lines.Add('')
[void]$lines.Add('| ID | Section | Level | Automation / exception | Remaining reason |')
[void]$lines.Add('| --- | --- | --- | --- | --- |')
foreach ($item in $items) {
    $mapping = if ($item.implementationStatus -eq 'manual-required') {
        "Manual: $($item.manualException)"
    }
    else {
        (@($item.automationTests | ForEach-Object { "$($_.suite)/$($_.test)" }) -join '<br>')
    }
    $reason = ([string]$item.remainingReason).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
    [void]$lines.Add("| $($item.id) | $($item.manualSection) | $($item.coverageLevel) | $mapping | $reason |")
}

$directory = Split-Path -Parent $OutputPath
if ($directory) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
$lines | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Coverage report: $([IO.Path]::GetFullPath($OutputPath))"
