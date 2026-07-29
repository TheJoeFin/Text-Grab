[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'ReleaseSignoff.Helpers.psm1') -Force
$root = Join-Path $PSScriptRoot ('artifacts\release-signoff-semantic-validation-' + [guid]::NewGuid().ToString('N'))

function Write-Fixture {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][object[]]$Results)

    $output = Join-Path $root "$Name\run\output"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    @{ results = $Results } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
    return Join-Path $root $Name
}

function Assert-Equal {
    param([Parameter(Mandatory)]$Actual, [Parameter(Mandatory)]$Expected, [Parameter(Mandatory)][string]$Message)

    if ($Actual -ne $Expected) { throw "$Message Expected '$Expected', got '$Actual'." }
}

try {
    $allPass = Write-Fixture -Name 'all-pass' -Results @(
        @{ suite = 'Smoke'; name = 'Starts'; outcome = 'pass'; details = ''; screenshot = $null }
    )
    $allPassDecision = Test-ReleaseSignoffUiLane -Summary (Get-ReleaseSignoffUiLaneSummary -ArtifactRoot $allPass) -LaneName 'all-pass'
    Assert-Equal $allPassDecision.outcome 'pass' 'All-pass lane should pass.'

    $passWithSkip = Write-Fixture -Name 'pass-with-expected-skip' -Results @(
        @{ suite = 'Smoke'; name = 'Starts'; outcome = 'pass'; details = ''; screenshot = $null },
        @{ suite = 'Capability'; name = 'Optional feature'; outcome = 'skip'; details = 'Capability absent.'; screenshot = $null }
    )
    $passWithSkipSummary = Get-ReleaseSignoffUiLaneSummary -ArtifactRoot $passWithSkip
    $passWithSkipDecision = Test-ReleaseSignoffUiLane -Summary $passWithSkipSummary -LaneName 'pass-with-expected-skip'
    Assert-Equal $passWithSkipDecision.outcome 'pass' 'Executed lane with an expected skip should pass.'
    Assert-Equal $passWithSkipSummary.skippedTests.Count 1 'Expected skip should be retained.'

    $allSkip = Write-Fixture -Name 'all-skip-requested' -Results @(
        @{ suite = 'Capability'; name = 'Required feature'; outcome = 'skip'; details = 'Capability absent.'; screenshot = $null }
    )
    $allSkipDecision = Test-ReleaseSignoffUiLane -Summary (Get-ReleaseSignoffUiLaneSummary -ArtifactRoot $allSkip) -LaneName 'requested capability lane'
    Assert-Equal $allSkipDecision.outcome 'fail' 'All-skipped requested lane must fail sign-off.'

    $failure = Write-Fixture -Name 'failure' -Results @(
        @{ suite = 'Smoke'; name = 'Starts'; outcome = 'fail'; details = 'Assertion failed.'; screenshot = $null }
    )
    $failureDecision = Test-ReleaseSignoffUiLane -Summary (Get-ReleaseSignoffUiLaneSummary -ArtifactRoot $failure) -LaneName 'failing lane'
    Assert-Equal $failureDecision.outcome 'fail' 'Failed UI test must fail sign-off.'

    Write-Output 'Release sign-off UI lane semantic validation passed.'
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
