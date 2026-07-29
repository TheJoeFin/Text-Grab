[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$ArtifactRoot = (Join-Path $PSScriptRoot 'artifacts\release-signoff'),
    [switch]$IncludeSystemIntegration,
    [switch]$IncludeCapabilities,
    [switch]$IncludePackagedVm,
    [switch]$Record,
    [switch]$SkipXunit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'ReleaseSignoff.Helpers.psm1') -Force
$runRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactRoot)) ("signoff-{0}-{1}" -f [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$steps = [System.Collections.Generic.List[object]]::new()
$appProjectPath = Join-Path $repositoryRoot 'Text-Grab\Text-Grab.csproj'
$appProjectText = Get-Content -LiteralPath $appProjectPath -Raw
$appVersion = if ($appProjectText -match '<Version>([^<]+)</Version>') { $matches[1] } else { 'unspecified' }

function Invoke-SignoffStep {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action,
        [Parameter(Mandatory)][string]$Artifact,
        [switch]$UiLane,
        [ValidateRange(1, [int]::MaxValue)][int]$MinimumExecuted = 1
    )

    $started = [DateTimeOffset]::UtcNow
    $outcome = 'pass'
    $details = $null
    $commandFailed = $false
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) {
            throw "Command exited with code $LASTEXITCODE."
        }
    }
    catch {
        $commandFailed = $true
        $details = $_.Exception.ToString()
    }
    $uiSummary = $null
    $uiDecision = $null
    if ($UiLane) {
        $uiSummary = Get-ReleaseSignoffUiLaneSummary -ArtifactRoot $Artifact
        $uiDecision = Test-ReleaseSignoffUiLane -Summary $uiSummary -MinimumExecuted $MinimumExecuted -LaneName $Name
        if ($commandFailed -or $uiDecision.outcome -ne 'pass') {
            $outcome = 'fail'
            $allDetails = @($details) + @($uiDecision.reasons)
            $details = ($allDetails | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join [Environment]::NewLine
        }
    }
    elseif ($commandFailed) {
        $outcome = 'fail'
    }

    $step = [ordered]@{
        name = $Name
        outcome = $outcome
        durationSeconds = [Math]::Round(([DateTimeOffset]::UtcNow - $started).TotalSeconds, 3)
        artifact = $Artifact
        details = $details
    }
    if ($UiLane) {
        $step.uiTests = [ordered]@{
            passed = $uiSummary.passed
            failed = $uiSummary.failed
            skipped = $uiSummary.skipped
            unknown = $uiSummary.unknown
            executed = $uiSummary.executed
            requiredMinimumExecuted = $uiDecision.minimumExecuted
            requiredMinimumMet = $uiDecision.minimumExecutedMet
            reports = $uiSummary.reports
            failedTests = $uiSummary.failedTests
            skippedTests = $uiSummary.skippedTests
            parseErrors = $uiSummary.parseErrors
        }
    }
    [void]$steps.Add($step)
}

$environment = [ordered]@{
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    configuration = $Configuration
    computerName = $env:COMPUTERNAME
    userName = $env:USERNAME
    os = [Environment]::OSVersion.VersionString
    dotnetSdk = (& dotnet --version 2>&1 | Out-String).Trim()
    gitRevision = (& git -C $repositoryRoot rev-parse HEAD 2>$null | Out-String).Trim()
    appProject = 'Text-Grab\Text-Grab.csproj'
    appVersion = $appVersion
    requestedLanes = [ordered]@{
        systemIntegration = [bool]$IncludeSystemIntegration
        capabilities = [bool]$IncludeCapabilities
        packagedVm = [bool]$IncludePackagedVm
    }
    disposableVmConfirmed = $env:TEXT_GRAB_DISPOSABLE_VM -eq '1'
    systemIntegrationRegistration = 'disabled; requires --automation-disposable-registration and TEXT_GRAB_DISPOSABLE_VM=1'
}

$coverageReport = Join-Path $runRoot 'coverage-report.md'
Invoke-SignoffStep -Name 'coverage' -Artifact $coverageReport -Action {
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Validate-Coverage.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Validate-Coverage.ps1 failed with exit code $LASTEXITCODE." }
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'New-CoverageReport.ps1') -OutputPath $coverageReport
    if ($LASTEXITCODE -ne 0) { throw "New-CoverageReport.ps1 failed with exit code $LASTEXITCODE." }
}

if (-not $SkipXunit) {
    $xunitDirectory = Join-Path $runRoot 'xunit'
    Invoke-SignoffStep -Name 'xunit' -Artifact $xunitDirectory -Action {
        & dotnet test (Join-Path $repositoryRoot 'Tests\Tests.csproj') -c $Configuration --results-directory $xunitDirectory --logger 'trx;LogFileName=release-signoff.trx' --nologo
    }
}

$standardDirectory = Join-Path $runRoot 'ui-standard'
$standardSuites = @('Harness', 'Lifecycle', 'CliProtocol', 'GrabFrame', 'EditText', 'QuickLookup', 'Settings', 'PatternsBulk')
Invoke-SignoffStep -Name 'ui-standard-safe' -Artifact $standardDirectory -UiLane -MinimumExecuted 1 -Action {
    $arguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Run-UiTests.ps1'), '-Configuration', $Configuration, '-ArtifactRoot', $standardDirectory, '-Suite', ($standardSuites -join ','))
    if ($Record) { $arguments += '-Record' }
    & pwsh @arguments
}

if ($IncludeCapabilities) {
    $capabilityDirectory = Join-Path $runRoot 'ui-capabilities'
    Invoke-SignoffStep -Name 'ui-capabilities' -Artifact $capabilityDirectory -UiLane -MinimumExecuted 1 -Action {
        $arguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Run-UiTests.ps1'), '-Configuration', $Configuration, '-ArtifactRoot', $capabilityDirectory, '-Suite', 'DisplayHardware,OcrCapability,NotificationsTtsShare,OsArchitecture')
        if ($Record) { $arguments += '-Record' }
        & pwsh @arguments
    }
}

if ($IncludeSystemIntegration) {
    $systemDirectory = Join-Path $runRoot 'ui-system-integration'
    Invoke-SignoffStep -Name 'ui-system-integration' -Artifact $systemDirectory -UiLane -MinimumExecuted 1 -Action {
        # Ordinary system integration intentionally omits the disposable-registration
        # argument and therefore cannot rewrite HKCU protocol/file associations.
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Run-UiTests.ps1') -Configuration $Configuration -ArtifactRoot $systemDirectory -Suite SystemIntegration -SystemIntegration
    }
}

if ($IncludePackagedVm) {
    $packageDirectory = Join-Path $runRoot 'packaged-vm'
    if ($env:TEXT_GRAB_DISPOSABLE_VM -ne '1') {
        [void]$steps.Add([ordered]@{
            name = 'packaged-vm'
            outcome = 'fail'
            durationSeconds = 0
            artifact = $packageDirectory
            details = 'Requested lane refused: set TEXT_GRAB_DISPOSABLE_VM=1 on a resettable VM before requesting the destructive package lane.'
            uiTests = [ordered]@{
                passed = 0
                failed = 0
                skipped = 0
                unknown = 0
                executed = 0
                requiredMinimumExecuted = 1
                requiredMinimumMet = $false
                reports = @()
                failedTests = @()
                skippedTests = @()
                parseErrors = @('The explicitly requested packaged VM lane was not run.')
            }
        })
    }
    else {
        Invoke-SignoffStep -Name 'packaged-vm' -Artifact $packageDirectory -UiLane -MinimumExecuted 1 -Action {
            & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Invoke-InteractiveCiLane.ps1') -Lane packaged -ArtifactRoot $packageDirectory -Configuration $Configuration
        }
    }
}

$uiSteps = @($steps | Where-Object { $_ -is [System.Collections.IDictionary] -and $_.Contains('uiTests') })
$result = [ordered]@{
    environment = $environment
    artifacts = [ordered]@{
        root = $runRoot
        coverageReport = $coverageReport
        standardUi = $standardDirectory
    }
    counts = [ordered]@{
        passed = @($steps | Where-Object outcome -eq 'pass').Count
        failed = @($steps | Where-Object outcome -eq 'fail').Count
        skipped = @($steps | Where-Object outcome -eq 'skip').Count
    }
    uiTests = [ordered]@{
        passed = @($uiSteps | ForEach-Object { $_.uiTests.passed } | Measure-Object -Sum).Sum
        failed = @($uiSteps | ForEach-Object { $_.uiTests.failed } | Measure-Object -Sum).Sum
        skipped = @($uiSteps | ForEach-Object { $_.uiTests.skipped } | Measure-Object -Sum).Sum
        executed = @($uiSteps | ForEach-Object { $_.uiTests.executed } | Measure-Object -Sum).Sum
        failedTests = @($uiSteps | ForEach-Object { $_.uiTests.failedTests })
        skippedTests = @($uiSteps | ForEach-Object { $_.uiTests.skippedTests })
        reports = @($uiSteps | ForEach-Object { $_.uiTests.reports })
    }
    steps = @($steps)
}
$jsonPath = Join-Path $runRoot 'release-signoff.json'
$markdownPath = Join-Path $runRoot 'release-signoff.md'
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$markdown = @(
    '# Text Grab release sign-off',
    '',
    "Generated: $($environment.generatedUtc)",
    '',
    '| Step | Outcome | Seconds | UI pass/fail/skip | Artifact |',
    '| --- | --- | ---: | --- | --- |'
)
foreach ($step in $steps) {
    $hasUiTests = $step -is [System.Collections.IDictionary] -and $step.Contains('uiTests')
    $uiCounts = if ($hasUiTests) { "$($step.uiTests.passed)/$($step.uiTests.failed)/$($step.uiTests.skipped)" } else { '-' }
    $markdown += "| $($step.name) | $($step.outcome) | $($step.durationSeconds) | $uiCounts | ``$($step.artifact)`` |"
}
$markdown += @(
    '',
    "Result: **$($result.counts.passed) passed, $($result.counts.failed) failed, $($result.counts.skipped) skipped**.",
    "UI tests: **$($result.uiTests.passed) passed, $($result.uiTests.failed) failed, $($result.uiTests.skipped) skipped, $($result.uiTests.executed) executed**.",
    '',
    "Coverage report: ``$coverageReport``",
    "Sign-off JSON: ``$jsonPath``",
    "Build: ``$Configuration``; app version: ``$($environment.appVersion)``; SDK: ``$($environment.dotnetSdk)``; revision: ``$($environment.gitRevision)``"
)
foreach ($step in $uiSteps) {
    $markdown += @('', "## $($step.name) UI evidence", "Required executed minimum: $($step.uiTests.requiredMinimumExecuted); met: $($step.uiTests.requiredMinimumMet).")
    foreach ($report in $step.uiTests.reports) {
        $markdown += "Report: ``$($report.resultsPath)``; JUnit: ``$($report.junitPath)``; environment: ``$($report.environmentPath)``; diagnostics: ``$($report.diagnosticsPath)``; screenshots: ``$($report.screenshotsPath)``; recordings: ``$($report.recordingsPath)``."
    }
    foreach ($failedTest in $step.uiTests.failedTests) {
        $markdown += "Failed: **$($failedTest.suite)/$($failedTest.name)** — $($failedTest.reason) (report: ``$($failedTest.resultPath)``)."
    }
    foreach ($skippedTest in $step.uiTests.skippedTests) {
        $markdown += "Skipped: **$($skippedTest.suite)/$($skippedTest.name)** — $($skippedTest.reason) (report: ``$($skippedTest.resultPath)``)."
    }
    foreach ($parseError in $step.uiTests.parseErrors) {
        $markdown += "Report error: $parseError"
    }
}
$markdown | Set-Content -LiteralPath $markdownPath -Encoding utf8
Write-Output "Sign-off JSON: $jsonPath"
Write-Output "Sign-off Markdown: $markdownPath"
if ($result.counts.failed -gt 0) { exit 1 }
