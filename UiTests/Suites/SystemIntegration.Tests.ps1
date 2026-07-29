Set-StrictMode -Version Latest

$script:SystemHelper = $null

function Require-SystemIntegration {
    param([Parameter(Mandatory)]$Context)

    if (-not $Context.SystemIntegration) {
        Skip-UiTest 'System integration is disabled. Re-run with -SystemIntegration on an isolated automation profile.'
    }
    if ($Context.ProfileDirectory -like "$env:APPDATA*" -or $Context.ProfileDirectory -like "$env:LOCALAPPDATA*") {
        Skip-UiTest "Refusing real-input automation because the profile is not isolated: $($Context.ProfileDirectory)"
    }
}

function Get-SystemIntegrationHelper {
    if ($null -ne $script:SystemHelper) {
        return $script:SystemHelper
    }

    $project = Join-Path $PSScriptRoot '..\TextGrab.SystemIntegrationHelper\TextGrab.SystemIntegrationHelper.csproj'
    $project = [IO.Path]::GetFullPath($project)
    & dotnet build $project -c Debug --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Skip-UiTest 'The system-integration helper could not be built on this machine.'
    }

    $candidate = Get-ChildItem -LiteralPath (Join-Path (Split-Path -Parent $project) 'bin\Debug') -Filter 'TextGrab.SystemIntegrationHelper.exe' -File -Recurse |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $candidate) {
        Skip-UiTest 'The system-integration helper executable was not produced.'
    }

    $script:SystemHelper = $candidate.FullName
    return $script:SystemHelper
}

function Invoke-SystemIntegrationHelper {
    param([Parameter(Mandatory)][string[]]$Arguments, [switch]$AllowFailure)

    $helper = Get-SystemIntegrationHelper
    $output = & $helper @Arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "System integration helper failed: $output"
    }
    return $output.Trim()
}

function Initialize-SystemIntegration {
    param([Parameter(Mandatory)]$Context)

    Require-SystemIntegration $Context
    if ($null -ne $Context.PSObject.Properties['SystemIntegrationPreflight']) {
        if (-not $Context.SystemIntegrationPreflight.Available) {
            Skip-UiTest $Context.SystemIntegrationPreflight.Reason
        }
        return
    }

    $output = Invoke-SystemIntegrationHelper -Arguments @('--preflight') -AllowFailure
    try {
        $preflight = $output | ConvertFrom-Json
    }
    catch {
        $preflight = $null
    }

    $available = $null -ne $preflight -and $preflight.userInteractive -and $preflight.inputDesktopAvailable -and $preflight.inputDesktop -eq 'Default'
    $reason = if ($available) {
        $null
    }
    elseif ($null -eq $preflight) {
        'The Windows input-desktop preflight did not return usable data; the session is unsupported for real-input tests.'
    }
    else {
        "The interactive desktop is unavailable or locked (desktop='$($preflight.inputDesktop)', error=$($preflight.error))."
    }

    $Context | Add-Member -NotePropertyName SystemIntegrationPreflight -NotePropertyValue ([pscustomobject]@{
        Available = $available
        Reason = $reason
    })
    if (-not $available) {
        Skip-UiTest $reason
    }
}

function Require-NoSystemIntegrationCollision {
    param([Parameter(Mandatory)]$Context, [Parameter(Mandatory)][string]$Scenario)

    $owned = @($Context.Processes | ForEach-Object { $_.Process.Id })
    $unrelated = @(Get-Process -Name 'Text-Grab' -ErrorAction SilentlyContinue |
        Where-Object { $_.Id -notin $owned })
    if ($unrelated.Count -gt 0) {
        $details = $unrelated | ForEach-Object { "$($_.Id):$($_.Path)" }
        Skip-UiTest "$Scenario is skipped because unrelated Text Grab process(es) are running: $($details -join '; '). No existing process will be altered."
    }
}

function Get-ExplorerTrayTarget {
    $explorer = Get-Process -Name explorer -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $explorer) {
        Skip-UiTest 'Explorer is not running, so notification-area UIA is unavailable.'
    }
    $trayWindow = @(Get-UiTestWindows -ProcessId $explorer.Id | Where-Object className -eq 'Shell_TrayWnd' | Select-Object -First 1)
    if ($trayWindow.Count -ne 1) {
        Skip-UiTest 'Explorer does not expose a Shell_TrayWnd notification-area HWND in this session.'
    }
    [pscustomobject]@{
        Kind = 'Explorer'
        ProcessId = $explorer.Id
        WindowHandle = [long]$trayWindow[0].hwnd
        WindowTitle = if ($null -ne $trayWindow[0].PSObject.Properties['title']) { [string]$trayWindow[0].title } else { '' }
    }
}

function Find-ExplorerTextGrabIcon {
    param([Parameter(Mandatory)]$Tray)

    $result = Invoke-UiTestWinApp -Arguments @(
        'ui', 'search', 'Text Grab', '--app', [string]$Tray.ProcessId, '--window', [string]$Tray.WindowHandle
    ) -Json
    $matches = @($result.matches | Where-Object {
        $_.automationId -eq 'NotifyItemIcon' -and $_.name -like 'Text Grab*'
    })
    if ($matches.Count -eq 0) {
        $hidden = Invoke-UiTestWinApp -Arguments @(
            'ui', 'search', 'Show Hidden Icons', '--app', [string]$Tray.ProcessId, '--window', [string]$Tray.WindowHandle
        ) -Json
        $hiddenIcon = @($hidden.matches | Where-Object { $_.automationId -eq 'SystemTrayIcon' } | Select-Object -First 1)
        if ($hiddenIcon.Count -eq 1) {
            Click-UiTestElement -Target $Tray -AutomationId ([string]$hiddenIcon[0].selector)
            Start-Sleep -Milliseconds 250
            $matches = foreach ($window in @(Get-UiTestWindows -ProcessId $Tray.ProcessId)) {
                $overflow = Invoke-UiTestWinApp -Arguments @(
                    'ui', 'search', 'Text Grab', '--app', [string]$Tray.ProcessId, '--window', [string]$window.hwnd
                ) -Json -AllowFailure
                @($overflow.matches | Where-Object {
                    $_.automationId -eq 'NotifyItemIcon' -and $_.name -like 'Text Grab*'
                })
            }
        }
    }
    if ($matches.Count -ne 1) {
        Skip-UiTest "Expected exactly one Text Grab notification-area icon, found $($matches.Count)."
    }
    return [string]$matches[0].selector
}

function New-SystemUiTarget {
    param([Parameter(Mandatory)][int]$ProcessId, [Parameter(Mandatory)]$Window)

    [pscustomobject]@{
        Kind = 'TextGrab'
        ProcessId = $ProcessId
        WindowHandle = [long]$Window.hwnd
        WindowTitle = [string]$Window.title
    }
}

function Get-SystemWindow {
    param(
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][string]$AutomationId,
        [int]$TimeoutSeconds = 15
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($candidateProcessId in @($ProcessId)) {
            foreach ($window in @(Get-UiTestWindows -ProcessId $candidateProcessId)) {
                $target = New-SystemUiTarget -ProcessId $candidateProcessId -Window $window
                try {
                    Wait-UiTestElement -Target $target -AutomationId $AutomationId -TimeoutSeconds 1
                    return $target
                }
                catch { }
            }
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for AutomationId '$AutomationId' from process $ProcessId."
}

function Get-SystemElementCenter {
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)][string]$AutomationId)

    $bounds = Get-UiTestProperty -Target $Target -AutomationId $AutomationId -Property 'BoundingRectangle'
    $numbers = @([regex]::Matches($bounds, '-?\d+(?:\.\d+)?') | ForEach-Object { [double]$_.Value })
    if ($numbers.Count -lt 4 -or $numbers[2] -lt 2 -or $numbers[3] -lt 2) {
        Skip-UiTest "WinApp did not expose a usable BoundingRectangle for '$AutomationId': $bounds"
    }
    [pscustomobject]@{
        X = [int][Math]::Round($numbers[0] + ($numbers[2] / 2))
        Y = [int][Math]::Round($numbers[1] + ($numbers[3] / 2))
        Width = [int][Math]::Round($numbers[2])
        Height = [int][Math]::Round($numbers[3])
    }
}

function Send-SystemHotkey {
    param([Parameter(Mandatory)]$Context, [Parameter(Mandatory)][string]$Keys)

    Focus-UiTestElement -Target $Context.Fixture -AutomationId 'PostGrabInputTarget'
    Send-UiTestKeys -Target $Context.Fixture -Keys $Keys -Via send-input -AllowSystemKeys
    Start-Sleep -Milliseconds 400
}

function Enable-SystemHotkeys {
    param([Parameter(Mandatory)]$Context)

    $settings = Start-UiTestProcess -Context $Context -FilePath $Context.TextGrab.Process.Path -Arguments @(
        '--automation-profile', $Context.ProfileDirectory, '--automation-system-integration', 'Settings'
    ) -Kind TextGrab -WindowTitle 'Settings'
    Wait-UiTestElement -Target $settings -AutomationId 'Settings.Nav.Shortcuts' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $settings -AutomationId 'Settings.Nav.Shortcuts'
    Wait-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.BackgroundToggle' -TimeoutSeconds $Context.TimeoutSeconds

    # These toggles use UIA rather than mutating user settings files, so their cleanup remains scoped to the disposable profile.
    Click-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.BackgroundToggle'
    Click-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.GlobalHotkeysToggle'
    Send-UiTestKeys -Target $settings -Keys '%{F4}' -Via send-input -AllowSystemKeys
    Assert-UiTestProcess -ProcessId $settings.ProcessId
    return $settings
}

function Close-SystemWindows {
    param([Parameter(Mandatory)]$Context, [string]$AutomationId)

    foreach ($window in @(Get-UiTestWindows -ProcessId $Context.TextGrab.ProcessId)) {
        $target = New-SystemUiTarget -ProcessId $Context.TextGrab.ProcessId -Window $window
        try {
            Wait-UiTestElement -Target $target -AutomationId $AutomationId -TimeoutSeconds 1
            Send-UiTestKeys -Target $target -Keys '%{F4}' -Via send-input -AllowSystemKeys
        }
        catch { }
    }
}

function Ensure-SystemEditor {
    param([Parameter(Mandatory)]$Context)

    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Direct-window real-input automation'
    try {
        return Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'EditTextWindow' -TimeoutSeconds 2
    }
    catch {
        $entry = @($Context.Processes | Where-Object Kind -eq 'TextGrab' | Select-Object -First 1)
        if ($entry.Count -ne 1) {
            throw 'No Text Grab executable is available for the system-integration editor.'
        }
        $editor = Start-UiTestProcess -Context $Context -FilePath $entry[0].Process.Path -Arguments @(
            '--automation-profile', $Context.ProfileDirectory, '--automation-system-integration', 'EditText'
        ) -Kind TextGrab -WindowTitle 'Edit Text'
        try {
            Wait-UiTestElement -Target $editor -AutomationId 'EditTextWindow' -TimeoutSeconds $Context.TimeoutSeconds
        }
        catch {
            Skip-UiTest 'The isolated automation instance could not create an Edit Text window because another Text Grab instance owns application startup. This environment cannot run direct-window system scenarios safely.'
        }
        return $editor
    }
}

# Suites are dot-sourced from the harness module's import function. Publish suite-local helpers so
# the registered scriptblocks still resolve them after that import scope has returned.
foreach ($name in @(
    'Require-SystemIntegration', 'Get-SystemIntegrationHelper', 'Invoke-SystemIntegrationHelper',
    'Initialize-SystemIntegration', 'Require-NoSystemIntegrationCollision', 'Get-ExplorerTrayTarget',
    'Find-ExplorerTextGrabIcon', 'New-SystemUiTarget', 'Get-SystemWindow',
    'Get-SystemElementCenter', 'Send-SystemHotkey', 'Enable-SystemHotkeys',
    'Close-SystemWindows', 'Ensure-SystemEditor'
)) {
    Set-Item -Path "function:global:$name" -Value (Get-Item -Path "function:$name").ScriptBlock
}

Register-UiTest -Suite 'SystemIntegration' -Name 'preflight-requires-isolated-unlocked-desktop' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    Assert-UiTestProcess -ProcessId $Context.Fixture.ProcessId
    Assert-UiTestWindow -ProcessId $Context.Fixture.ProcessId -WindowHandle $Context.Fixture.WindowHandle
}

Register-UiTest -Suite 'SystemIntegration' -Name 'global-hotkeys-launch-actions-and-clean-up' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Global-hotkey activation'
    Enable-SystemHotkeys $Context | Out-Null

    $actions = @(
        @{ Keys = '#+f'; Id = 'FullscreenGrabWindow'; Name = 'fullscreen' },
        @{ Keys = '#+g'; Id = 'GrabFrameWindow'; Name = 'grab-frame' },
        @{ Keys = '#+e'; Id = 'EditTextWindow'; Name = 'edit-text' },
        @{ Keys = '#+q'; Id = 'QuickLookupWindow'; Name = 'quick-lookup' }
    )
    foreach ($action in $actions) {
        Send-SystemHotkey -Context $Context -Keys $action.Keys
        $window = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId $action.Id -TimeoutSeconds $Context.TimeoutSeconds
        Assert-UiTestWindow -ProcessId $window.ProcessId -WindowHandle $window.WindowHandle
        Save-UiTestScreenshot -Context $Context -Target $window -Name "hotkey-$($action.Name)" -CaptureScreen | Out-Null
        Send-UiTestKeys -Target $window -Keys '%{F4}' -Via send-input -AllowSystemKeys
    }
}

Register-UiTest -Suite 'SystemIntegration' -Name 'global-hotkey-disable-stops-registration' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Global-hotkey disable'
    $settings = Enable-SystemHotkeys $Context
    Invoke-UiTestElement -Target $settings -AutomationId 'Settings.Nav.Shortcuts'
    Wait-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.GlobalHotkeysToggle' -TimeoutSeconds $Context.TimeoutSeconds
    Click-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.GlobalHotkeysToggle'
    Send-UiTestKeys -Target $settings -Keys '%{F4}' -Via send-input -AllowSystemKeys
    Send-SystemHotkey -Context $Context -Keys '#+f'
    $fullscreen = @(Get-UiTestWindows -ProcessId $Context.TextGrab.ProcessId | ForEach-Object {
        New-SystemUiTarget -ProcessId $Context.TextGrab.ProcessId -Window $_
    } | Where-Object {
        try { Wait-UiTestElement -Target $_ -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds 1; $true } catch { $false }
    })
    if ($fullscreen.Count -ne 0) {
        throw 'Fullscreen Grab launched after global hotkeys were disabled.'
    }
}

Register-UiTest -Suite 'SystemIntegration' -Name 'hotkey-conflict-does-not-steal-owned-combination' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Global-hotkey conflict reporting'
    $ready = Join-Path $Context.LogsDirectory 'hotkey-conflict-ready.txt'
    $holder = Start-Process -FilePath (Get-SystemIntegrationHelper) -ArgumentList @('--hold-hotkey', '12', '70', $ready) -PassThru
    try {
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Context.TimeoutSeconds)
        while (-not (Test-Path -LiteralPath $ready) -and [DateTimeOffset]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Test-Path -LiteralPath $ready)) {
            Skip-UiTest 'Unable to reserve the conflict hotkey; this desktop reserves the selected combination.'
        }
        Enable-SystemHotkeys $Context | Out-Null
        Wait-UiTestDiagnosticEvent -Path (Join-Path $Context.ProfileDirectory 'diagnostics\events.jsonl') -EventName 'hotkey-registration-failed' -TimeoutSeconds $Context.TimeoutSeconds
        Assert-UiTestProcess -ProcessId $holder.Id
    }
    finally {
        if (-not $holder.HasExited) {
            Stop-Process -Id $holder.Id -Force
            $holder.WaitForExit(5000) | Out-Null
        }
        Assert-UiTestProcess -ProcessId $holder.Id -Exited
    }
}

Register-UiTest -Suite 'SystemIntegration' -Name 'hotkey-rebinding-record-control-updates-active-registration' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Global-hotkey rebinding'
    $settings = Enable-SystemHotkeys $Context
    Invoke-UiTestElement -Target $settings -AutomationId 'Settings.Nav.Shortcuts'
    Wait-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.FullscreenGrab.Record' -TimeoutSeconds $Context.TimeoutSeconds
    Click-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.FullscreenGrab.Record'
    Send-UiTestKeys -Target $settings -AutomationId 'Settings.Shortcuts.FullscreenGrab.Record' -Keys '^+{F12}' -Via send-input -AllowSystemKeys
    Click-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.FullscreenGrab.Record'
    Send-UiTestKeys -Target $settings -Keys '%{F4}' -Via send-input -AllowSystemKeys
    Send-SystemHotkey -Context $Context -Keys '^+{F12}'
    $overlay = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds
    Send-UiTestKeys -Target $overlay -Keys '%{F4}' -Via send-input -AllowSystemKeys
}

Register-UiTest -Suite 'SystemIntegration' -Name 'fullscreen-real-region-empty-retry-and-cancel-inputs' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    $editor = Ensure-SystemEditor $Context
    Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.FullscreenGrab'
    $overlay = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds
    $canvas = Get-SystemElementCenter -Target $overlay -AutomationId 'FullscreenGrab.SelectionCanvas'
    $fixture = Get-SystemElementCenter -Target $Context.Fixture -AutomationId 'KnownTextDisplay'

    # Select a known visible fixture area using physical mouse input; result verification is through the real clipboard.
    Invoke-SystemIntegrationHelper -Arguments @('--drag',
        [string]($fixture.X - 120), [string]($fixture.Y - 25),
        [string]($fixture.X + 260), [string]($fixture.Y + 45)) | Out-Null
    Start-Sleep -Milliseconds 700
    if (-not [System.Windows.Clipboard]::ContainsText()) {
        Save-UiTestScreenshot -Context $Context -Target $overlay -Name 'fullscreen-region-no-clipboard' -CaptureScreen | Out-Null
        throw 'Fullscreen region selection did not produce clipboard text.'
    }

    Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.FullscreenGrab'
    $overlay = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-SystemIntegrationHelper -Arguments @('--drag', [string]($canvas.X - 20), [string]($canvas.Y - 20), [string]($canvas.X + 20), [string]($canvas.Y + 20)) | Out-Null
    Wait-UiTestElement -Target $overlay -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-SystemIntegrationHelper -Arguments @('--escape') | Out-Null
    Wait-UiTestElement -Target $overlay -AutomationId 'FullscreenGrabWindow' -Gone -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'SystemIntegration' -Name 'clipboard-text-image-and-file-drop-activation-restores-content' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Clipboard hotkey activation'
    Enable-SystemHotkeys $Context | Out-Null
    $snapshot = Get-UiTestClipboardSnapshot
    try {
        Invoke-SystemIntegrationHelper -Arguments @('--set-text', "clipboard fixture $($Context.RunId)") | Out-Null
        Send-SystemHotkey -Context $Context -Keys '#+^v'
        $clipboardEditor = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'EditTextWindow' -TimeoutSeconds $Context.TimeoutSeconds
        Wait-UiTestElement -Target $clipboardEditor -AutomationId 'EditText.Editor' -TimeoutSeconds $Context.TimeoutSeconds
        Close-SystemWindows -Context $Context -AutomationId 'EditTextWindow'

        Invoke-SystemIntegrationHelper -Arguments @('--set-image') | Out-Null
        Send-SystemHotkey -Context $Context -Keys '#+^v'
        $frame = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'GrabFrameWindow' -TimeoutSeconds $Context.TimeoutSeconds
        Wait-UiTestElement -Target $frame -AutomationId 'GrabFrame.Image' -TimeoutSeconds $Context.TimeoutSeconds
        Send-UiTestKeys -Target $frame -Keys '%{F4}' -Via send-input -AllowSystemKeys

        $image = Join-Path $Context.ProfileDirectory 'clipboard-drop.png'
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\Tests\Images\font_sample.png') -Destination $image -Force
        Invoke-SystemIntegrationHelper -Arguments @('--set-files', $image) | Out-Null
        Send-SystemHotkey -Context $Context -Keys '#+^v'
        $frame = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'GrabFrameWindow' -TimeoutSeconds $Context.TimeoutSeconds
        Wait-UiTestElement -Target $frame -AutomationId 'GrabFrame.Image' -TimeoutSeconds $Context.TimeoutSeconds
    }
    finally {
        Restore-UiTestClipboardSnapshot -Snapshot $snapshot
    }
}

Register-UiTest -Suite 'SystemIntegration' -Name 'clipboard-watcher-observes-text-and-restores-original-clipboard' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    $snapshot = Get-UiTestClipboardSnapshot
    try {
        $editor = Ensure-SystemEditor $Context
        Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.ClipboardWatcher'
        $expected = "clipboard watcher $($Context.RunId)"
        Invoke-SystemIntegrationHelper -Arguments @('--set-text', $expected) | Out-Null
        Wait-UiTestElement -Target $editor -AutomationId 'EditText.Editor' -Value $expected -Contains -TimeoutSeconds $Context.TimeoutSeconds
        Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.ClipboardWatcher'
    }
    finally {
        Restore-UiTestClipboardSnapshot -Snapshot $snapshot
    }
}

Register-UiTest -Suite 'SystemIntegration' -Name 'fullscreen-output-controls-and-word-peers-have-stable-selectors' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    $editor = Ensure-SystemEditor $Context
    Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.FullscreenGrab'
    $overlay = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds
    foreach ($automationId in @(
        'FullscreenGrab.StandardModeToggle', 'FullscreenGrab.SingleLineToggle',
        'FullscreenGrab.TableToggle', 'FullscreenGrab.SendToEditTextToggle',
        'FullscreenGrab.SelectionStyle', 'FullscreenGrab.AcceptSelectionButton'
    )) {
        Wait-UiTestElement -Target $overlay -AutomationId $automationId -TimeoutSeconds $Context.TimeoutSeconds
    }
    # The dynamic word peer IDs are intentionally coordinate-based so OCR duplicate text is unambiguous.
    # A word is created only after a completed OCR selection; the region suite exercises that path with real input.
    Send-UiTestKeys -Target $overlay -Keys '%{F4}' -Via send-input -AllowSystemKeys
}

Register-UiTest -Suite 'SystemIntegration' -Name 'real-ole-file-drop-targets-edit-text-and-grab-frame' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    $image = Join-Path $Context.ProfileDirectory 'drag-drop.png'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\Tests\Images\font_sample.png') -Destination $image -Force
    $editor = Ensure-SystemEditor $Context
    $dropSourceReady = Join-Path $Context.LogsDirectory 'edit-text-drop-ready.txt'
    $dropSource = Start-Process -FilePath (Get-SystemIntegrationHelper) -ArgumentList @('--drag-files', $dropSourceReady, $image) -PassThru
    try {
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Context.TimeoutSeconds)
        while (-not (Test-Path -LiteralPath $dropSourceReady) -and [DateTimeOffset]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Test-Path -LiteralPath $dropSourceReady)) {
            Skip-UiTest 'The real OLE drag source could not create its HWND on this desktop.'
        }
        $target = Get-SystemElementCenter -Target $editor -AutomationId 'EditText.Editor'
        Invoke-SystemIntegrationHelper -Arguments @('--drag', '50', '40', [string]$target.X, [string]$target.Y) | Out-Null
        Wait-UiTestElement -Target $editor -AutomationId 'EditText.Editor' -TimeoutSeconds $Context.TimeoutSeconds
    }
    finally {
        if (-not $dropSource.HasExited) {
            Stop-Process -Id $dropSource.Id -Force
            $dropSource.WaitForExit(5000) | Out-Null
        }
        Assert-UiTestProcess -ProcessId $dropSource.Id -Exited
    }

    Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.GrabFrame'
    $frame = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'GrabFrameWindow' -TimeoutSeconds $Context.TimeoutSeconds
    $dropSourceReady = Join-Path $Context.LogsDirectory 'grab-frame-drop-ready.txt'
    $dropSource = Start-Process -FilePath (Get-SystemIntegrationHelper) -ArgumentList @('--drag-files', $dropSourceReady, $image) -PassThru
    try {
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Context.TimeoutSeconds)
        while (-not (Test-Path -LiteralPath $dropSourceReady) -and [DateTimeOffset]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Test-Path -LiteralPath $dropSourceReady)) {
            Skip-UiTest 'The real OLE drag source could not create its HWND on this desktop.'
        }
        $target = Get-SystemElementCenter -Target $frame -AutomationId 'GrabFrame.Content'
        Invoke-SystemIntegrationHelper -Arguments @('--drag', '50', '40', [string]$target.X, [string]$target.Y) | Out-Null
        Wait-UiTestElement -Target $frame -AutomationId 'GrabFrame.Image' -TimeoutSeconds $Context.TimeoutSeconds
    }
    finally {
        if (-not $dropSource.HasExited) {
            Stop-Process -Id $dropSource.Id -Force
            $dropSource.WaitForExit(5000) | Out-Null
        }
        Assert-UiTestProcess -ProcessId $dropSource.Id -Exited
    }
}

Register-UiTest -Suite 'SystemIntegration' -Name 'external-insertion-returns-to-fixture-focus-target' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    $editor = Ensure-SystemEditor $Context
    $expected = "external insertion $($Context.RunId)"
    Set-UiTestValue -Target $editor -AutomationId 'EditText.Editor' -Value $expected
    Focus-UiTestElement -Target $Context.Fixture -AutomationId 'PostGrabInputTarget'
    Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.CloseAndInsert'
    Wait-UiTestElement -Target $Context.Fixture -AutomationId 'ReceivedInputText' -Value $expected -Contains -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'SystemIntegration' -Name 'fullscreen-smoke-repetition-has-no-window-or-temp-leak' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    $temporaryDirectory = Join-Path $Context.ProfileDirectory 'temp'
    $before = @(Get-ChildItem -LiteralPath $temporaryDirectory -File -Recurse -ErrorAction SilentlyContinue).Count
    $editor = Ensure-SystemEditor $Context
    foreach ($iteration in 1..3) {
        Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.FullscreenGrab'
        $overlay = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds
        Invoke-SystemIntegrationHelper -Arguments @('--escape') | Out-Null
        Wait-UiTestElement -Target $overlay -AutomationId 'FullscreenGrabWindow' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    }
    $after = @(Get-ChildItem -LiteralPath $temporaryDirectory -File -Recurse -ErrorAction SilentlyContinue).Count
    if ($after -gt ($before + 3)) {
        throw "Fullscreen smoke left an unbounded number of temporary files ($before -> $after)."
    }
    Assert-UiTestProcess -ProcessId $Context.Fixture.ProcessId
    Assert-UiTestProcess -ProcessId $Context.TextGrab.ProcessId
}

Register-UiTest -Suite 'SystemIntegration' -Name 'tray-disable-removes-explorer-icon-and-hotkey-registration' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Tray disable cleanup'
    $settings = Enable-SystemHotkeys $Context
    $explorerTray = Get-ExplorerTrayTarget
    Find-ExplorerTextGrabIcon -Tray $explorerTray | Out-Null
    Invoke-UiTestElement -Target $settings -AutomationId 'Settings.Nav.Shortcuts'
    Wait-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.BackgroundToggle' -TimeoutSeconds $Context.TimeoutSeconds
    Click-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.BackgroundToggle'
    Start-Sleep -Milliseconds 500
    $result = Invoke-UiTestWinApp -Arguments @(
        'ui', 'search', 'Text Grab', '--app', [string]$explorerTray.ProcessId, '--window', [string]$explorerTray.WindowHandle
    ) -Json
    $remaining = @($result.matches | Where-Object {
        $_.automationId -eq 'NotifyItemIcon' -and $_.name -like 'Text Grab*'
    })
    if ($remaining.Count -ne 0) {
        throw 'Text Grab notification icon remained visible in Explorer after disabling background mode.'
    }
    Send-UiTestKeys -Target $settings -Keys '%{F4}' -Via send-input -AllowSystemKeys
}

Register-UiTest -Suite 'SystemIntegration' -Name 'tray-background-and-process-cleanup' -Script {
    param($Context)
    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Tray notification-area interaction'
    $settings = Enable-SystemHotkeys $Context
    Invoke-UiTestElement -Target $settings -AutomationId 'Settings.Nav.Shortcuts'
    Wait-UiTestElement -Target $settings -AutomationId 'Settings.Shortcuts.BackgroundToggle' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestProcess -ProcessId $settings.ProcessId
    $ownedTray = Get-SystemWindow -ProcessId $settings.ProcessId -AutomationId 'NotifyIconWindow' -TimeoutSeconds $Context.TimeoutSeconds
    $explorerTray = Get-ExplorerTrayTarget
    $iconSelector = Find-ExplorerTextGrabIcon -Tray $explorerTray
    Click-UiTestElement -Target $explorerTray -AutomationId $iconSelector -Right
    Wait-UiTestElement -Target $ownedTray -AutomationId 'NotifyIcon.Menu.EditText' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $ownedTray -AutomationId 'NotifyIcon.Menu.EditText'
    Get-SystemWindow -ProcessId $settings.ProcessId -AutomationId 'EditTextWindow' -TimeoutSeconds $Context.TimeoutSeconds | Out-Null
    Click-UiTestElement -Target $explorerTray -AutomationId $iconSelector -Right
    Wait-UiTestElement -Target $ownedTray -AutomationId 'NotifyIcon.Menu.Close' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $ownedTray -AutomationId 'NotifyIcon.Menu.Close'
    Start-Sleep -Milliseconds 500
    Assert-UiTestProcess -ProcessId $settings.ProcessId -Exited
}
