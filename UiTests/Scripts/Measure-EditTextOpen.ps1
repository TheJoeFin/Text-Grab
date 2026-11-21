<#
.SYNOPSIS
Measures how long an Edit Text Window takes to open, and the process's memory, with the pane layout
(v5 E4a) off and on, for a short and a large document.

.DESCRIPTION
Each run starts Text Grab in a fresh automation profile with EtwPaneLayout seeded Off or On and a text
file on the command line, and waits for the window's etw-ready diagnostics event: the first idle after the
window has rendered and finished loading the file, when its editor takes input. It records:

  windowMs    from the window's construction to etw-ready (in-process stopwatch)
  processMs   from process start to etw-ready (includes app startup, the same on both paths)
  workingSetMb, privateMb   the process's memory after -SettleSeconds more
  warmWindowMs   (short document only) a second, empty window opened from the Window menu in the same
                 process, after the first one's code is loaded and compiled

Off and On alternate within each run so drift affects both alike. Prints the median of each measure and
writes every sample to -OutputPath when given. Windows it opens take focus.

.EXAMPLE
pwsh -NoProfile -File UiTests\Scripts\Measure-EditTextOpen.ps1 -TextGrabPath Text-Grab\bin\ARM64\Release\net10.0-windows10.0.22621.0\Text-Grab.exe -Runs 7
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TextGrabPath,
    [ValidateRange(1, 50)][int]$Runs = 5,
    [int]$LargeLines = 50000,
    [int]$SettleSeconds = 3,
    [int]$TimeoutSeconds = 90,
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$uiTestsRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $uiTestsRoot 'TextGrab.UiTestHarness.psm1') -Force -DisableNameChecking

$textGrabExe = [IO.Path]::GetFullPath($TextGrabPath)
if (-not (Test-Path -LiteralPath $textGrabExe -PathType Leaf)) { throw "Text Grab executable was not found: $textGrabExe" }

$work = Join-Path $uiTestsRoot ('artifacts\measure-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$documents = [ordered]@{
    short = Join-Path $work 'short.txt'
    large = Join-Path $work 'large.txt'
}
[IO.File]::WriteAllText($documents.short, "1 + 1`r`n2 * 3`r`nA short note.`r`n")
$builder = [Text.StringBuilder]::new()
for ($line = 1; $line -le $LargeLines; $line++) { [void]$builder.Append("Line ${line}: the quick brown fox jumps over $line lazy dogs = $line * 3`r`n") }
[IO.File]::WriteAllText($documents.large, $builder.ToString())

function Wait-ReadyEvents {
    param([string]$EventsPath, [int]$Count)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if (Test-Path -LiteralPath $EventsPath) {
            $ready = @(Get-Content -LiteralPath $EventsPath | ForEach-Object { try { $_ | ConvertFrom-Json } catch { } } | Where-Object { $_.eventName -eq 'etw-ready' })
            if ($ready.Count -ge $Count) { return $ready }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Count etw-ready event(s) in $EventsPath."
}

function Get-Median {
    param([double[]]$Values)

    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return [double]::NaN }
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}

$samples = [System.Collections.Generic.List[object]]::new()
for ($run = 1; $run -le $Runs; $run++) {
    foreach ($document in $documents.Keys) {
        foreach ($paneLayout in @('Off', 'On')) {
            $profile = Join-Path $work "profile-$run-$document-$paneLayout"
            New-Item -ItemType Directory -Path $profile -Force | Out-Null
            @{ settings = @{
                    FirstRun = $false; RunInTheBackground = $false; StartupOnLogin = $false; GlobalHotkeysEnabled = $false
                    ShowToast = $false; DefaultLaunch = 'EditText'; LastUsedLang = 'en-US'; UseTesseract = $false
                    UiAutomationEnabled = $false; WindowsAiDescriptionEnabled = $false; EnableFileBackedManagedSettings = $true
                    EtwPaneLayout = $paneLayout
                } } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $profile 'seed.json') -Encoding utf8

            $events = Join-Path $profile 'diagnostics\events.jsonl'
            $process = Start-Process -FilePath $textGrabExe -ArgumentList ('--automation-profile "{0}" "{1}"' -f $profile, $documents[$document]) -PassThru -WorkingDirectory (Split-Path -Parent $textGrabExe)
            try {
                $ready = @(Wait-ReadyEvents -EventsPath $events -Count 1)[0]
                $processMs = ([DateTimeOffset]$ready.timestampUtc - [DateTimeOffset]$process.StartTime).TotalMilliseconds
                Start-Sleep -Seconds $SettleSeconds
                $process.Refresh()
                $workingSet = $process.WorkingSet64 / 1MB
                $private = $process.PrivateMemorySize64 / 1MB

                $warmMs = $null
                if ($document -eq 'short') {
                    $window = Wait-UiTestWindow -ProcessId $process.Id -Title 'Edit Text' -TimeoutSeconds $TimeoutSeconds
                    Invoke-UiTestWinApp -Arguments @('ui', 'invoke', 'WindowMenuItem', '--window', [string]$window.hwnd) -Json | Out-Null
                    Invoke-UiTestWinApp -Arguments @('ui', 'invoke', 'NewWindowMenuItem', '--app', [string]$process.Id) -Json | Out-Null
                    $warmMs = [double](@(Wait-ReadyEvents -EventsPath $events -Count 2)[1].details.elapsedMs)
                }

                $samples.Add([pscustomobject]@{
                    run = $run
                    document = $document
                    paneLayout = $paneLayout
                    windowMs = [Math]::Round([double]$ready.details.elapsedMs, 1)
                    processMs = [Math]::Round($processMs, 1)
                    warmWindowMs = if ($null -ne $warmMs) { [Math]::Round($warmMs, 1) } else { $null }
                    workingSetMb = [Math]::Round($workingSet, 1)
                    privateMb = [Math]::Round($private, 1)
                    textLength = [int]$ready.details.textLength
                })
                Write-Host ("run {0} {1,-5} {2,-3}: window {3,7:N1} ms, process {4,7:N1} ms, warm {5}, working set {6,6:N1} MB, private {7,6:N1} MB" -f `
                        $run, $document, $paneLayout, [double]$ready.details.elapsedMs, $processMs, $(if ($null -ne $warmMs) { '{0:N1} ms' -f $warmMs } else { '-' }), $workingSet, $private)
            }
            finally {
                if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
                $process.WaitForExit(10000) | Out-Null
            }
        }
    }
}

$summary = foreach ($group in ($samples | Group-Object document, paneLayout)) {
    $first = $group.Group[0]
    [pscustomobject]@{
        document = $first.document
        paneLayout = $first.paneLayout
        runs = $group.Count
        windowMs = [Math]::Round((Get-Median @($group.Group.windowMs)), 1)
        processMs = [Math]::Round((Get-Median @($group.Group.processMs)), 1)
        warmWindowMs = if ($first.document -eq 'short') { [Math]::Round((Get-Median @($group.Group.warmWindowMs)), 1) } else { $null }
        workingSetMb = [Math]::Round((Get-Median @($group.Group.workingSetMb)), 1)
        privateMb = [Math]::Round((Get-Median @($group.Group.privateMb)), 1)
    }
}

$summary | Format-Table -AutoSize | Out-String -Width 200 | Write-Output
if ($OutputPath) {
    [ordered]@{ textGrab = $textGrabExe; samples = $samples; medians = $summary } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
}
