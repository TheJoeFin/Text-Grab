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

function Set-DeterministicTextHistory {
    <#
    .SYNOPSIS
        Seeds the isolated profile's Edit Text history (what the recent items pane and the tray flyout list).
        Texts are written newest first, a minute apart. Call before starting Text Grab with the profile.
    #>
    param(
        [Parameter(Mandatory)][string]$ProfileDirectory,
        [Parameter(Mandatory)][string[]]$Texts
    )

    $history = Join-Path $ProfileDirectory 'history'
    New-Item -ItemType Directory -Path $history -Force | Out-Null
    $now = [DateTimeOffset]::Now
    $items = for ($i = 0; $i -lt $Texts.Count; $i++) {
        [ordered]@{
            ID = [guid]::NewGuid().ToString()
            TextContent = $Texts[$i]
            CaptureDateTime = $now.AddMinutes(-($i + 1)).ToString('o')
            EditorMode = 'Text'
        }
    }
    # Piped, so each item is one element; -InputObject with an array wraps it in a second array.
    @($items) | ConvertTo-Json -Depth 4 -AsArray | Set-Content -LiteralPath (Join-Path $history 'HistoryTextOnly.json') -Encoding utf8
}

function Copy-DeterministicMermaidFixture {
    <#
    .SYNOPSIS
        Copies a file from UiTests\Fixtures\Mermaid into the isolated profile's temp folder and returns the copy's path,
        so a test opens (and may edit) the copy rather than the file in the repository.
    #>
    param(
        [Parameter(Mandatory)][string]$ProfileDirectory,
        [Parameter(Mandatory)][string]$Name
    )

    $temp = Join-Path $ProfileDirectory 'temp'
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    $source = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSScriptRoot) "Fixtures\Mermaid\$Name"))
    $destination = Join-Path $temp $Name
    Copy-Item -LiteralPath $source -Destination $destination -Force
    return $destination
}

function Wait-DeterministicProperty {
    <#
    .SYNOPSIS
        Polls a UI Automation property of an element until $Predicate, given the property's value, returns true.
        Returns the value that satisfied it.
    #>
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Property,
        [Parameter(Mandatory)][scriptblock]$Predicate,
        [Parameter(Mandatory)][string]$Description,
        [int]$TimeoutSeconds = 30
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $last = $null
    do {
        try {
            $last = Get-UiTestProperty -Target $Target -AutomationId $AutomationId -Property $Property
            if (& $Predicate $last) { return $last }
        }
        catch {
            # The element may not exist yet, or between two renders.
            $last = "<$($_.Exception.Message)>"
        }
        Start-Sleep -Milliseconds 300
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out after $TimeoutSeconds seconds waiting for $Description (AutomationId '$AutomationId', $Property). Last value: '$last'."
}

function Assert-DeterministicLeftOf {
    <#
    .SYNOPSIS
        Fails unless the element $Left is left of the element $Right on screen.
    #>
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$Left,
        [Parameter(Mandatory)][string]$Right
    )

    $leftBox = (Invoke-UiTestWinApp -Arguments @('ui', 'inspect', $Left, '--window', [string]$Target.WindowHandle, '--depth', '0') -Json).windows[0].elements[0]
    $rightBox = (Invoke-UiTestWinApp -Arguments @('ui', 'inspect', $Right, '--window', [string]$Target.WindowHandle, '--depth', '0') -Json).windows[0].elements[0]
    if ([int]$leftBox.x -ge [int]$rightBox.x) {
        throw "'$Left' should be left of '$Right'; x=$($leftBox.x) and x=$($rightBox.x)."
    }
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
    'Set-DeterministicTextHistory',
    'Copy-DeterministicMermaidFixture',
    'Wait-DeterministicProperty',
    'Assert-DeterministicLeftOf',
    'Assert-DeterministicAutomationHealthy',
    'Open-DeterministicMenu',
    'Set-DeterministicInteractiveValue'
)
