Set-StrictMode -Version Latest

function Get-ReleaseSignoffUiLaneSummary {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ArtifactRoot)

    $resultFiles = @(Get-ChildItem -LiteralPath $ArtifactRoot -Filter 'results.json' -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName)
    $reports = [System.Collections.Generic.List[object]]::new()
    $failedTests = [System.Collections.Generic.List[object]]::new()
    $skippedTests = [System.Collections.Generic.List[object]]::new()
    $parseErrors = [System.Collections.Generic.List[string]]::new()
    $passed = 0
    $failed = 0
    $skipped = 0
    $unknown = 0

    foreach ($resultFile in $resultFiles) {
        try {
            $document = Get-Content -LiteralPath $resultFile.FullName -Raw | ConvertFrom-Json -ErrorAction Stop
            if ($null -eq $document.results) {
                throw 'The report does not contain a results array.'
            }

            $reportPassed = 0
            $reportFailed = 0
            $reportSkipped = 0
            $reportUnknown = 0
            foreach ($test in @($document.results)) {
                switch ([string]$test.outcome) {
                    'pass' { $passed++; $reportPassed++ }
                    'fail' {
                        $failed++
                        $reportFailed++
                        [void]$failedTests.Add([ordered]@{
                            suite = [string]$test.suite
                            name = [string]$test.name
                            reason = [string]$test.details
                            resultPath = $resultFile.FullName
                            screenshot = [string]$test.screenshot
                        })
                    }
                    'skip' {
                        $skipped++
                        $reportSkipped++
                        [void]$skippedTests.Add([ordered]@{
                            suite = [string]$test.suite
                            name = [string]$test.name
                            reason = [string]$test.details
                            resultPath = $resultFile.FullName
                            screenshot = [string]$test.screenshot
                        })
                    }
                    default { $unknown++; $reportUnknown++ }
                }
            }

            [void]$reports.Add([ordered]@{
                resultsPath = $resultFile.FullName
                junitPath = Join-Path $resultFile.Directory.FullName 'junit.xml'
                environmentPath = Join-Path $resultFile.Directory.FullName 'environment.json'
                diagnosticsPath = Join-Path $resultFile.Directory.Parent.FullName 'diagnostics\events.jsonl'
                screenshotsPath = Join-Path $resultFile.Directory.Parent.FullName 'screenshots'
                recordingsPath = Join-Path $resultFile.Directory.Parent.FullName 'recordings'
                passed = $reportPassed
                failed = $reportFailed
                skipped = $reportSkipped
                unknown = $reportUnknown
            })
        }
        catch {
            [void]$parseErrors.Add("$($resultFile.FullName): $($_.Exception.Message)")
        }
    }

    [pscustomobject]@{
        artifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
        reportCount = $reports.Count
        reports = @($reports)
        passed = $passed
        failed = $failed
        skipped = $skipped
        unknown = $unknown
        executed = $passed + $failed
        failedTests = @($failedTests)
        skippedTests = @($skippedTests)
        parseErrors = @($parseErrors)
    }
}

function Test-ReleaseSignoffUiLane {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Summary,
        [ValidateRange(1, [int]::MaxValue)][int]$MinimumExecuted = 1,
        [string]$LaneName = 'UI lane'
    )

    $reasons = [System.Collections.Generic.List[string]]::new()
    if ($Summary.reportCount -eq 0) {
        [void]$reasons.Add("No results.json report was produced under '$($Summary.artifactRoot)'.")
    }
    foreach ($parseError in @($Summary.parseErrors)) {
        [void]$reasons.Add("Unreadable UI report: $parseError")
    }
    if ($Summary.unknown -gt 0) {
        [void]$reasons.Add("$($Summary.unknown) $LaneName test result(s) have an unknown outcome.")
    }
    if ($Summary.failed -gt 0) {
        [void]$reasons.Add("$($Summary.failed) $LaneName test(s) failed.")
    }
    if ($Summary.executed -lt $MinimumExecuted) {
        [void]$reasons.Add("$LaneName executed $($Summary.executed) test(s); its required minimum is $MinimumExecuted. All-skipped or unavailable requested lanes are not sign-off passes.")
    }

    [pscustomobject]@{
        outcome = if ($reasons.Count -eq 0) { 'pass' } else { 'fail' }
        minimumExecuted = $MinimumExecuted
        minimumExecutedMet = $Summary.executed -ge $MinimumExecuted
        reasons = @($reasons)
    }
}

Export-ModuleMember -Function Get-ReleaseSignoffUiLaneSummary, Test-ReleaseSignoffUiLane
