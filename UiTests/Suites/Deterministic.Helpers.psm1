Set-StrictMode -Version Latest

function Get-DeterministicTextGrabExecutable {
    param([Parameter(Mandatory)]$Context)

    $process = Get-Process -Id $Context.TextGrab.ProcessId -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace($process.Path)) {
        throw "Could not resolve Text Grab executable for process $($Context.TextGrab.ProcessId)."
    }

    return $process.Path
}

function Start-DeterministicTextGrab {
    param(
        [Parameter(Mandatory)]$Context,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)][string]$WindowTitle,
        [string]$ProfileDirectory = $Context.ProfileDirectory
    )

    $launchArguments = @('--automation-profile', $ProfileDirectory) + $Arguments
    return Start-UiTestProcess -Context $Context -FilePath (Get-DeterministicTextGrabExecutable $Context) `
        -Arguments $launchArguments -Kind TextGrab -WindowTitle $WindowTitle
}

function Start-FullscreenGrabTarget {
    param(
        [Parameter(Mandatory)]$Context,
        [switch]$SystemIntegration,
        [string]$ProfileDirectory = $Context.ProfileDirectory
    )

    $executable = Get-DeterministicTextGrabExecutable $Context
    $arguments = @('--automation-profile', $ProfileDirectory)
    if ($SystemIntegration) { $arguments += '--automation-system-integration' }
    $arguments += 'Fullscreen'
    $argumentLine = ($arguments | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }) -join ' '

    # Launch Fullscreen Grab as its own process straight from the command line and register it
    # for run cleanup without disturbing the shared editor instance ($Context.TextGrab).
    $process = Start-Process -FilePath $executable -ArgumentList $argumentLine -PassThru -WorkingDirectory (Split-Path -Parent $executable)
    [void]$Context.Processes.Add([pscustomobject]@{ Kind = 'TextGrab'; Process = $process })

    # The overlay is a transparent, owned WPF window: WinApp cannot reach its element tree
    # through the window HWND, and neither the window-root AutomationId nor the bare selection
    # Canvas is queryable. Hook the overlay by process id (AppScope) and confirm readiness by
    # waiting on the capture-surface background image, which is always present. (The capture
    # toolbar auto-hides for the default selection style until the pointer is over a
    # foregrounded overlay, so it is not a reliable readiness signal.)
    $target = [pscustomobject]@{
        Kind = 'TextGrab'
        Process = $process
        ProcessId = $process.Id
        WindowHandle = $null
        WindowTitle = 'Text Grab'
        AppScope = $true
    }
    Wait-UiTestElement -Target $target -AutomationId 'FullscreenGrab.BackgroundImage' -TimeoutSeconds $Context.TimeoutSeconds
    $window = Wait-UiTestWindow -ProcessId $process.Id -Title 'Text Grab' -TimeoutSeconds $Context.TimeoutSeconds
    $target.WindowHandle = [long]$window.hwnd
    return $target
}

function Stop-FullscreenGrabTarget {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)]$Target
    )

    # A dedicated Fullscreen process exits on Cancel/Escape but lingers after a completed grab
    # (its post-grab window keeps the app alive). Force the owned, disposable process to exit so
    # it can never overlap the next overlay: a second overlay launched while another Text Grab
    # process is alive comes up toolbar-less because of Text Grab's multi-overlay coordination.
    $process = $Target.Process
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit($Context.TimeoutSeconds * 1000) | Out-Null
    }
}

function Wait-FullscreenGrabDismissed {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)]$Target
    )

    # Dismissing the overlay closes the last window of the dedicated process, which then exits
    # on last-window-close. If the process lingers (e.g. a tray host owns shutdown), fall back
    # to asserting the overlay UI is gone.
    if (-not $Target.Process.WaitForExit($Context.TimeoutSeconds * 1000)) {
        Wait-UiTestElement -Target $Target -AutomationId 'FullscreenGrab.BackgroundImage' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    }
}

function New-DeterministicTarget {
    param(
        [Parameter(Mandatory)]$Process,
        [Parameter(Mandatory)][string]$Title,
        [Parameter(Mandatory)]$Context
    )

    $window = Wait-UiTestWindow -ProcessId $Process.ProcessId -Title $Title -TimeoutSeconds $Context.TimeoutSeconds
    return [pscustomobject]@{
        Kind = 'TextGrab'
        Process = $Process.Process
        ProcessId = $Process.ProcessId
        WindowHandle = [long]$window.hwnd
        WindowTitle = [string]$window.title
    }
}

function New-DeterministicSeedProfile {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Name,
        [hashtable]$Settings = @{}
    )

    $profile = Join-Path $Context.OutputDirectory "profile-$Name"
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    $seedSettings = @{
        FirstRun = $false
        RunInTheBackground = $false
        StartupOnLogin = $false
        GlobalHotkeysEnabled = $false
        ShowToast = $false
        DefaultLaunch = 'EditText'
        LastUsedLang = 'en-US'
        UseTesseract = $false
        UiAutomationEnabled = $false
        WindowsAiDescriptionEnabled = $false
        EnableFileBackedManagedSettings = $true
    }
    foreach ($entry in $Settings.GetEnumerator()) {
        $seedSettings[$entry.Key] = $entry.Value
    }

    @{ settings = $seedSettings } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $profile 'seed.json') -Encoding utf8
    return $profile
}

function Assert-DeterministicAutomationHealthy {
    param([Parameter(Mandatory)]$Context)

    Assert-UiTestUserProfileUnchanged -Context $Context
    $failure = Join-Path $Context.ProfileDirectory 'diagnostics\failure.json'
    Assert-UiTestFile -Path $failure -Absent
}

function Open-DeterministicMenu {
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$MenuAutomationId,
        [Parameter(Mandatory)][string]$ChildAutomationId,
        [Parameter(Mandatory)]$Context
    )

    Set-UiTestForegroundWindow -Target $Target
    Click-UiTestElement -Target $Target -AutomationId $MenuAutomationId
    Wait-UiTestElement -Target $Target -AutomationId $ChildAutomationId -TimeoutSeconds $Context.TimeoutSeconds
}

function Set-DeterministicInteractiveValue {
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Value
    )

    Set-UiTestForegroundWindow -Target $Target
    Click-UiTestElement -Target $Target -AutomationId $AutomationId
    Send-UiTestKeys -Target $Target -Keys 'ctrl+a delete' -Via send-input
    Send-UiTestKeys -Target $Target -Keys $Value -Via send-input -Verbatim
}

Export-ModuleMember -Function @(
    'Get-DeterministicTextGrabExecutable',
    'Start-DeterministicTextGrab',
    'Start-FullscreenGrabTarget',
    'Stop-FullscreenGrabTarget',
    'Wait-FullscreenGrabDismissed',
    'New-DeterministicTarget',
    'New-DeterministicSeedProfile',
    'Assert-DeterministicAutomationHealthy',
    'Open-DeterministicMenu',
    'Set-DeterministicInteractiveValue'
)
