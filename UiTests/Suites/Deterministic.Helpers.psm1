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
    'New-DeterministicTarget',
    'New-DeterministicSeedProfile',
    'Assert-DeterministicAutomationHealthy',
    'Open-DeterministicMenu',
    'Set-DeterministicInteractiveValue'
)
