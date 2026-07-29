[CmdletBinding()]
param(
    [string]$Path = (Join-Path $PSScriptRoot 'coverage.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RegisteredUiTests {
    param([Parameter(Mandatory)][string]$SuiteDirectory)

    $tests = @{}
    foreach ($file in Get-ChildItem -LiteralPath $SuiteDirectory -Filter '*.Tests.ps1' -File) {
        $content = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($match in [regex]::Matches($content, "Register-UiTest\s+-Suite\s+'([^']+)'\s+-Name\s+'([^']+)'")) {
            $key = '{0}|{1}' -f $match.Groups[1].Value, $match.Groups[2].Value
            if ($tests.ContainsKey($key)) {
                throw "Duplicate registered UI test: $key"
            }
            $tests[$key] = $file.FullName
        }
    }
    return $tests
}

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Coverage inventory was not found: $Path"
}

$content = Get-Content -LiteralPath $Path -Raw
if (-not (Test-Json -Json $content)) {
    throw "Coverage inventory is not valid JSON: $Path"
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$inventory = $content | ConvertFrom-Json -AsHashtable
$items = @($inventory.items)
$validClassifications = @(
    'existing unit test',
    'winapp UIA',
    'real-input automation',
    'fixture-assisted automation',
    'packaged/VM lane',
    'hardware/display lane',
    'manual-only'
)
$validStatuses = @('implemented', 'manual-required')
$validCoverageLevels = @('full', 'partial', 'contract', 'capability-gated', 'manual')
$registeredTests = Get-RegisteredUiTests -SuiteDirectory (Join-Path $PSScriptRoot 'Suites')

if ($items.Count -ne 154) {
    throw "Expected 154 manual checks, found $($items.Count)."
}

$duplicateIds = @($items | Group-Object id | Where-Object Count -gt 1)
if ($duplicateIds.Count -gt 0) {
    throw "Duplicate coverage IDs: $($duplicateIds.Name -join ', ')"
}

foreach ($item in $items) {
    foreach ($property in 'id', 'manualSection', 'description', 'coverageClassification', 'automationLane', 'implementationStatus', 'coverageLevel', 'remainingReason') {
        if ([string]::IsNullOrWhiteSpace([string]$item[$property])) {
            throw "Coverage item is missing ${property}: $($item.id)"
        }
    }

    if ($item.id -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') {
        throw "Coverage item has an unstable ID format: $($item.id)"
    }

    if ($item.manualSection -notmatch '^(?:[1-9]|1[0-9]|20)\. ') {
        throw "Coverage item has an invalid manual section: $($item.id)"
    }

    if ($item.coverageClassification -notin $validClassifications) {
        throw "Coverage item has an invalid classification: $($item.id)"
    }

    if ($item.implementationStatus -notin $validStatuses) {
        throw "Coverage item is unclassified or planned: $($item.id) ($($item.implementationStatus))"
    }

    if ($item.coverageLevel -notin $validCoverageLevels) {
        throw "Coverage item has an invalid coverage level: $($item.id)"
    }

    if ($null -eq $item.currentCoverage -or $item.currentCoverage -isnot [array]) {
        throw "Coverage item must contain a currentCoverage array: $($item.id)"
    }

    foreach ($reference in $item.currentCoverage) {
        $relativeFile = ([string]$reference -split '::', 2)[0]
        $fullPath = Join-Path $repositoryRoot $relativeFile
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Coverage item references a missing xUnit/source file: $($item.id): $reference"
        }
        if ($reference -like '*::*') {
            $member = ([string]$reference -split '::', 2)[1]
            if ((Get-Content -LiteralPath $fullPath -Raw) -notmatch "\b$([regex]::Escape($member))\s*\(") {
                throw "Coverage item references a missing xUnit/source member: $($item.id): $reference"
            }
        }
    }

    if ($item.implementationStatus -eq 'manual-required') {
        if ($item.coverageClassification -ne 'manual-only' -or $item.coverageLevel -ne 'manual' -or [string]::IsNullOrWhiteSpace([string]$item.manualException)) {
            throw "Manual-required item must be a documented subjective manual exception: $($item.id)"
        }
        continue
    }

    $automationTests = @($item.automationTests)
    if ($automationTests.Count -eq 0) {
        throw "Implemented item has no automation test mapping: $($item.id)"
    }
    foreach ($automationTest in $automationTests) {
        foreach ($property in 'suite', 'test', 'file', 'coverageLevel', 'remainingReason') {
            if ([string]::IsNullOrWhiteSpace([string]$automationTest[$property])) {
                throw "Automation mapping is missing ${property}: $($item.id)"
            }
        }
        $key = '{0}|{1}' -f $automationTest.suite, $automationTest.test
        if (-not $registeredTests.ContainsKey($key)) {
            throw "Automation mapping references an unregistered test: $($item.id): $key"
        }
        $expectedFile = [IO.Path]::GetFullPath($registeredTests[$key])
        $mappedFile = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $automationTest.file))
        if ($expectedFile -ne $mappedFile -or -not (Test-Path -LiteralPath $mappedFile -PathType Leaf)) {
            throw "Automation mapping has an invalid suite file: $($item.id): $($automationTest.file)"
        }
        if ($automationTest.coverageLevel -notin $validCoverageLevels -or $automationTest.coverageLevel -eq 'manual') {
            throw "Automation mapping has an invalid coverage level: $($item.id)"
        }
    }
}

$statusCounts = $items | Group-Object implementationStatus | ForEach-Object { "$($_.Name)=$($_.Count)" }
$levelCounts = $items | Group-Object coverageLevel | ForEach-Object { "$($_.Name)=$($_.Count)" }
Write-Output "Validated $($items.Count) coverage items with unique IDs; $($statusCounts -join ', '); $($levelCounts -join ', ')."
