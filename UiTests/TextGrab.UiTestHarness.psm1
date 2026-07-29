Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'Capability.Preflight.psm1') -Force -DisableNameChecking

$script:RegisteredTests = [System.Collections.Generic.List[object]]::new()
$script:RequiredWinAppCommands = @(
    'list-windows', 'wait-for', 'get-value', 'get-property', 'set-value',
    'invoke', 'click', 'drag', 'send-keys', 'focus', 'screenshot', 'record'
)

function ConvertTo-UiTestArgument {
    param([Parameter(Mandatory)][string]$Value)

    '"' + $Value.Replace('"', '\"') + '"'
}

function New-UiTestRunContext {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ArtifactRoot,
        [switch]$SystemIntegration,
        [switch]$Record,
        [int]$TimeoutSeconds = 30
    )

    $timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
    $runRoot = Join-Path ([IO.Path]::GetFullPath($ArtifactRoot)) "run-$timestamp-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    $directories = @{
        Root = $runRoot
        Profile = (Join-Path $runRoot 'profile')
        Output = (Join-Path $runRoot 'output')
        Screenshots = (Join-Path $runRoot 'screenshots')
        Recordings = (Join-Path $runRoot 'recordings')
        Logs = (Join-Path $runRoot 'logs')
    }

    foreach ($path in $directories.Values) {
        [void](New-Item -ItemType Directory -Path $path -Force)
    }

    $seed = [ordered]@{
        settings = [ordered]@{
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
    }
    $seedPath = Join-Path $directories.Profile 'seed.json'
    $seed | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $seedPath -Encoding utf8

    [pscustomobject]@{
        RunId = Split-Path $runRoot -Leaf
        StartedUtc = [DateTimeOffset]::UtcNow
        Root = $directories.Root
        ProfileDirectory = $directories.Profile
        OutputDirectory = $directories.Output
        ScreenshotsDirectory = $directories.Screenshots
        RecordingsDirectory = $directories.Recordings
        LogsDirectory = $directories.Logs
        SeedPath = $seedPath
        FixtureStatePath = (Join-Path $directories.Logs 'fixture-state.jsonl')
        SystemIntegration = [bool]$SystemIntegration
        Record = [bool]$Record
        TimeoutSeconds = $TimeoutSeconds
        UserProfileRoot = $null
        UserProfileFingerprint = $null
        Processes = [System.Collections.Generic.List[object]]::new()
        Results = [System.Collections.Generic.List[object]]::new()
        Capabilities = $null
        TextGrab = $null
        Fixture = $null
    }
}

function Test-WinAppCli {
    [CmdletBinding()]
    param()

    $command = Get-Command winapp -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw 'WinApp CLI was not found. Install WinApp CLI v0.5.0 before running UI tests.'
    }

    $versionText = (& winapp --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $versionText -notmatch '^(?<major>0)\.(?<minor>5)\.(?<patch>\d+)(?:[-+].*)?$') {
        throw "WinApp CLI v0.5.x is required; found '$versionText'. Install the v0.5.0-compatible WinApp CLI."
    }

    $schemaText = (& winapp ui --cli-schema 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "WinApp CLI v$versionText does not expose the UI automation schema: $schemaText"
    }

    try {
        $schema = $schemaText | ConvertFrom-Json
    }
    catch {
        throw "WinApp CLI returned an invalid UI schema: $($_.Exception.Message)"
    }

    if ($schema.version -notmatch '^0\.5\.') {
        throw "WinApp UI schema version '$($schema.version)' is incompatible; expected v0.5.x."
    }

    $available = @($schema.subcommands.psobject.Properties.Name)
    $missing = @($script:RequiredWinAppCommands | Where-Object { $_ -notin $available })
    if ($missing.Count -gt 0) {
        throw "WinApp CLI v$versionText is missing required UI commands: $($missing -join ', ')."
    }

    [pscustomobject]@{
        Version = $versionText
        SchemaVersion = [string]$schema.schemaVersion
        Commands = $available
    }
}

function Invoke-UiTestWinApp {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$Json,
        [switch]$AllowFailure
    )

    $argumentsToRun = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in $Arguments) {
        [void]$argumentsToRun.Add($argument)
    }
    if ($Json -and $argumentsToRun -notcontains '--json') {
        [void]$argumentsToRun.Add('--json')
    }
    if (-not $Json -and $argumentsToRun -notcontains '--quiet' -and $argumentsToRun -notcontains '-q') {
        [void]$argumentsToRun.Add('--quiet')
    }

    $output = (& winapp @argumentsToRun 2>&1 | Out-String).Trim()
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "winapp $($argumentsToRun -join ' ') failed with exit code ${exitCode}: $output"
    }

    if (-not $Json) {
        return $output
    }

    if ([string]::IsNullOrWhiteSpace($output)) {
        if ($exitCode -ne 0) { return $null }
        return [pscustomobject]@{}
    }

    try {
        return $output | ConvertFrom-Json
    }
    catch {
        if ($AllowFailure) { return $null }
        throw "winapp returned non-JSON output for '$($Arguments -join ' ')': $output"
    }
}

function Get-UiTestWindows {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$ProcessId)

    # Windows PowerShell preserves ConvertFrom-Json's array as a single pipeline
    # object; explicitly enumerate it so the filter sees window objects.
    $windows = @(Invoke-UiTestWinApp -Arguments @('ui', 'list-windows', '--app', [string]$ProcessId) -Json | ForEach-Object { $_ })
    @($windows | Where-Object {
        $_.PSObject.Properties.Name -contains 'processId' -and [int]$_.processId -eq $ProcessId
    })
}

function Wait-UiTestWindow {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$ProcessId,
        [string]$Title,
        [int]$TimeoutSeconds = 30
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $matches = @(Get-UiTestWindows -ProcessId $ProcessId)
        if (-not [string]::IsNullOrWhiteSpace($Title)) {
            $matches = @($matches | Where-Object { $_.title -like "*$Title*" })
        }
        if ($matches.Count -eq 1) {
            return $matches[0]
        }
        if ($matches.Count -gt 1) {
            throw "Process $ProcessId has multiple matching windows. Target an explicit HWND: $($matches.hwnd -join ', ')."
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out after $TimeoutSeconds seconds waiting for a window from process $ProcessId$(if ($Title) { " matching '$Title'" })."
}

function New-UiTestWindowTarget {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][long]$WindowHandle,
        [string]$WindowTitle,
        [ValidateSet('TextGrab', 'Fixture', 'Dialog')][string]$Kind = 'Dialog'
    )

    [pscustomobject]@{
        Kind = $Kind
        Process = Get-Process -Id $ProcessId -ErrorAction Stop
        ProcessId = $ProcessId
        WindowHandle = $WindowHandle
        WindowTitle = $WindowTitle
    }
}

function Wait-UiTestDialog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][string]$Title,
        [int]$TimeoutSeconds = 30
    )

    $window = Wait-UiTestWindow -ProcessId $ProcessId -Title $Title -TimeoutSeconds $TimeoutSeconds
    return New-UiTestWindowTarget -ProcessId $ProcessId -WindowHandle ([long]$window.hwnd) -WindowTitle ([string]$window.title)
}

function Set-UiTestForegroundWindow {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Target)

    if ($null -eq ('TextGrab.UiTests.NativeWindow' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace TextGrab.UiTests
{
    public static class NativeWindow
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(uint processId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        public static bool ClickButtonByCaption(IntPtr dialogHandle, string caption)
        {
            IntPtr button = IntPtr.Zero;
            EnumChildWindows(dialogHandle, (hWnd, _) =>
            {
                var className = new System.Text.StringBuilder(32);
                var text = new System.Text.StringBuilder(256);
                GetClassName(hWnd, className, className.Capacity);
                GetWindowText(hWnd, text, text.Capacity);
                if (className.ToString().IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.ToString().IndexOf(caption, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    button = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            if (button == IntPtr.Zero)
                return false;
            SendMessage(button, 0x00F5, IntPtr.Zero, IntPtr.Zero);
            return true;
        }
    }
}
'@
    }

    $handle = [IntPtr]$Target.WindowHandle
    [void][TextGrab.UiTests.NativeWindow]::ShowWindowAsync($handle, 9)
    [void][TextGrab.UiTests.NativeWindow]::BringWindowToTop($handle)
    [void][TextGrab.UiTests.NativeWindow]::AllowSetForegroundWindow([uint32]::MaxValue)
    $foreground = [TextGrab.UiTests.NativeWindow]::GetForegroundWindow()
    $foregroundProcess = [uint32]0
    $foregroundThread = [TextGrab.UiTests.NativeWindow]::GetWindowThreadProcessId($foreground, [ref]$foregroundProcess)
    $currentThread = [TextGrab.UiTests.NativeWindow]::GetCurrentThreadId()
    $attached = $foregroundThread -ne 0 -and [TextGrab.UiTests.NativeWindow]::AttachThreadInput($currentThread, $foregroundThread, $true)
    try {
        [void][TextGrab.UiTests.NativeWindow]::SetForegroundWindow($handle)
        [void][TextGrab.UiTests.NativeWindow]::BringWindowToTop($handle)
    }
    finally {
        if ($attached) {
            [void][TextGrab.UiTests.NativeWindow]::AttachThreadInput($currentThread, $foregroundThread, $false)
        }
    }
    if ([TextGrab.UiTests.NativeWindow]::GetForegroundWindow() -ne $handle) {
        throw "Could not bring HWND $($Target.WindowHandle) to the foreground for targeted interactive UI testing."
    }
    Start-Sleep -Milliseconds 150
}

function Invoke-UiTestNativeDialogButton {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)][string]$Caption)

    if ($null -eq ('TextGrab.UiTests.NativeWindow' -as [type])) {
        Set-UiTestForegroundWindow -Target $Target
    }
    if (-not [TextGrab.UiTests.NativeWindow]::ClickButtonByCaption(([IntPtr]$Target.WindowHandle), $Caption)) {
        throw "Native dialog HWND $($Target.WindowHandle) has no Button captioned '$Caption'."
    }
}

function Get-UiTestTargetArguments {
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$AutomationId
    )

    if ([string]::IsNullOrWhiteSpace([string]$Target.WindowHandle)) {
        throw "No HWND is available while targeting AutomationId '$AutomationId'."
    }
    @($AutomationId, '--window', [string]$Target.WindowHandle)
}

function Wait-UiTestElement {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$AutomationId,
        [int]$TimeoutSeconds = 30,
        [string]$Value,
        [switch]$Contains,
        [switch]$Gone
    )

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in (Get-UiTestTargetArguments -Target $Target -AutomationId $AutomationId)) { [void]$arguments.Add($argument) }
    [void]$arguments.Add('--timeout')
    [void]$arguments.Add([string]($TimeoutSeconds * 1000))
    if ($PSBoundParameters.ContainsKey('Value')) {
        [void]$arguments.Add('--value')
        [void]$arguments.Add($Value)
    }
    if ($Contains) { [void]$arguments.Add('--contains') }
    if ($Gone) { [void]$arguments.Add('--gone') }
    Invoke-UiTestWinApp -Arguments (@('ui', 'wait-for') + @($arguments)) -Json | Out-Null
}

function Get-UiTestValue {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)][string]$AutomationId)

    $result = Invoke-UiTestWinApp -Arguments (@('ui', 'get-value') + (Get-UiTestTargetArguments -Target $Target -AutomationId $AutomationId)) -Json
    if ($result -is [string]) { return $result }
    $valueProperty = $result.PSObject.Properties['value']
    if ($null -ne $valueProperty) { return [string]$valueProperty.Value }
    $valueProperty = $result.PSObject.Properties['Value']
    if ($null -ne $valueProperty) { return [string]$valueProperty.Value }
    $valueProperty = $result.PSObject.Properties['text']
    if ($null -ne $valueProperty) { return [string]$valueProperty.Value }
    return [string]$result
}

function Assert-UiTestValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Expected,
        [switch]$Contains
    )

    $actual = Get-UiTestValue -Target $Target -AutomationId $AutomationId
    $matches = if ($Contains) { $actual.Contains($Expected) } else { $actual -ceq $Expected }
    if (-not $matches) {
        throw "AutomationId '$AutomationId' value assertion failed. Expected$(if ($Contains) { ' to contain' }) '$Expected'; actual '$actual'."
    }
}

function Get-UiTestProperty {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Property
    )

    $arguments = @('ui', 'get-property') + (Get-UiTestTargetArguments -Target $Target -AutomationId $AutomationId) + @('--property', $Property)
    $result = Invoke-UiTestWinApp -Arguments $arguments -Json
    if ($result -is [string]) { return $result }
    $valueProperty = $result.PSObject.Properties['value']
    if ($null -ne $valueProperty) { return [string]$valueProperty.Value }
    $valueProperty = $result.PSObject.Properties['Value']
    if ($null -ne $valueProperty) { return [string]$valueProperty.Value }
    return [string]$result
}

function Assert-UiTestProperty {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Property,
        [Parameter(Mandatory)][string]$Expected
    )

    $actual = Get-UiTestProperty -Target $Target -AutomationId $AutomationId -Property $Property
    if ($actual -cne $Expected) {
        throw "AutomationId '$AutomationId' property '$Property' expected '$Expected'; actual '$actual'."
    }
}

function Set-UiTestValue {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)][string]$AutomationId, [Parameter(Mandatory)][string]$Value)

    $arguments = @('ui', 'set-value', $AutomationId, $Value, '--window', [string]$Target.WindowHandle)
    Invoke-UiTestWinApp -Arguments $arguments -Json | Out-Null
}

function Invoke-UiTestElement {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)][string]$AutomationId)

    Invoke-UiTestWinApp -Arguments (@('ui', 'invoke') + (Get-UiTestTargetArguments -Target $Target -AutomationId $AutomationId)) -Json | Out-Null
}

function Click-UiTestElement {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)][string]$AutomationId, [switch]$Double, [switch]$Right)

    $arguments = @('ui', 'click') + (Get-UiTestTargetArguments -Target $Target -AutomationId $AutomationId)
    if ($Double) { $arguments += '--double' }
    if ($Right) { $arguments += '--right' }
    Invoke-UiTestWinApp -Arguments $arguments -Json | Out-Null
}

function Focus-UiTestElement {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Target, [Parameter(Mandatory)][string]$AutomationId)

    Invoke-UiTestWinApp -Arguments (@('ui', 'focus') + (Get-UiTestTargetArguments -Target $Target -AutomationId $AutomationId)) -Json | Out-Null
}

function Send-UiTestKeys {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$Keys,
        [string]$AutomationId,
        [ValidateSet('post-message', 'send-input')][string]$Via = 'post-message',
        [switch]$Verbatim,
        [switch]$AllowSystemKeys
    )

    $arguments = @('ui', 'send-keys', $Keys, '--window', [string]$Target.WindowHandle, '--via', $Via)
    if ($AutomationId) { $arguments += @('--target', $AutomationId) }
    if ($Verbatim) { $arguments += '--verbatim' }
    if ($AllowSystemKeys) { $arguments += '--allow-system-keys' }
    Invoke-UiTestWinApp -Arguments $arguments -Json | Out-Null
}

function Drag-UiTestElement {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$FromAutomationId,
        [Parameter(Mandatory)][string]$ToAutomationId,
        [int]$HoldMilliseconds = 0,
        [int]$DwellMilliseconds = 100
    )

    $arguments = @('ui', 'drag', $FromAutomationId, $ToAutomationId, '--window', [string]$Target.WindowHandle, '--hold-ms', [string]$HoldMilliseconds, '--dwell-ms', [string]$DwellMilliseconds)
    Invoke-UiTestWinApp -Arguments $arguments -Json | Out-Null
}

function Save-UiTestScreenshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$Name,
        [switch]$CaptureScreen
    )

    $safeName = $Name -replace '[^A-Za-z0-9._-]', '_'
    $path = Join-Path $Context.ScreenshotsDirectory "$safeName.png"
    $arguments = @('ui', 'screenshot', '--window', [string]$Target.WindowHandle, '--output', $path)
    if ($CaptureScreen) { $arguments += '--capture-screen' }
    Invoke-UiTestWinApp -Arguments $arguments -Json | Out-Null
    return $path
}

function Save-UiTestRecording {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$Name,
        [int]$DurationSeconds = 5,
        [switch]$CaptureScreen
    )

    if (-not $Context.Record) { return $null }
    $safeName = $Name -replace '[^A-Za-z0-9._-]', '_'
    $path = Join-Path $Context.RecordingsDirectory "$safeName.mp4"
    $arguments = @('ui', 'record', '--window', [string]$Target.WindowHandle, '--duration-sec', [string]$DurationSeconds, '--output', $path)
    if ($CaptureScreen) { $arguments += '--capture-screen' }
    Invoke-UiTestWinApp -Arguments $arguments -Json | Out-Null
    return $path
}

function Get-UiTestClipboardSnapshot {
    [CmdletBinding()]
    param()

    Add-Type -AssemblyName PresentationCore
    Add-Type -AssemblyName WindowsBase
    [System.Windows.Clipboard]::ContainsText() | Out-Null
    [pscustomobject]@{
        DataObject = [System.Windows.Clipboard]::GetDataObject()
        HasText = [System.Windows.Clipboard]::ContainsText()
        Text = if ([System.Windows.Clipboard]::ContainsText()) { [System.Windows.Clipboard]::GetText() } else { $null }
    }
}

function Restore-UiTestClipboardSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Snapshot)

    Add-Type -AssemblyName PresentationCore
    Add-Type -AssemblyName WindowsBase
    if ($null -ne $Snapshot.DataObject) {
        [System.Windows.Clipboard]::SetDataObject($Snapshot.DataObject, $true)
    }
    elseif ($Snapshot.HasText) {
        [System.Windows.Clipboard]::SetText([string]$Snapshot.Text)
    }
    else {
        [System.Windows.Clipboard]::Clear()
    }
}

function Assert-UiTestFile {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path, [switch]$Absent)

    $exists = Test-Path -LiteralPath $Path
    if ($Absent -and $exists) { throw "Expected file to be absent: $Path" }
    if (-not $Absent -and -not $exists) { throw "Expected file was not found: $Path" }
}

function Assert-UiTestProcess {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$ProcessId, [switch]$Exited)

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($Exited -and $null -ne $process) { throw "Process $ProcessId is still running." }
    if (-not $Exited -and $null -eq $process) { throw "Process $ProcessId is not running." }
}

function Assert-UiTestWindow {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int]$ProcessId, [Parameter(Mandatory)][long]$WindowHandle)

    $match = @(Get-UiTestWindows -ProcessId $ProcessId | Where-Object { [long]$_.hwnd -eq $WindowHandle })
    if ($match.Count -ne 1) { throw "Expected HWND $WindowHandle for process $ProcessId was not found." }
}

function Get-UiTestDirectoryFingerprint {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return '__absent__' }
    $entries = Get-ChildItem -LiteralPath $Path -File -Recurse -Force |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path = $_.FullName.Substring($Path.Length).TrimStart('\')
                hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        }
    $json = @($entries) | ConvertTo-Json -Compress -Depth 4
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    ([Security.Cryptography.SHA256]::Create().ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
}

function Assert-UiTestUserProfileUnchanged {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Context)

    if ([string]::IsNullOrWhiteSpace([string]$Context.UserProfileRoot)) {
        throw 'The harness did not capture a normal Text Grab profile path.'
    }
    $current = Get-UiTestDirectoryFingerprint -Path $Context.UserProfileRoot
    if ($current -ne $Context.UserProfileFingerprint) {
        throw "The normal Text Grab user profile changed during the harness run: $($Context.UserProfileRoot)"
    }
}

function Start-UiTestProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)][ValidateSet('TextGrab', 'Fixture')][string]$Kind,
        [string]$WindowTitle
    )

    $argumentLine = ($Arguments | ForEach-Object { ConvertTo-UiTestArgument $_ }) -join ' '
    $process = Start-Process -FilePath $FilePath -ArgumentList $argumentLine -PassThru -WorkingDirectory (Split-Path -Parent $FilePath)
    [void]$Context.Processes.Add([pscustomobject]@{ Kind = $Kind; Process = $process })
    $window = Wait-UiTestWindow -ProcessId $process.Id -Title $WindowTitle -TimeoutSeconds $Context.TimeoutSeconds
    $target = [pscustomobject]@{
        Kind = $Kind
        Process = $process
        ProcessId = $process.Id
        WindowHandle = [long]$window.hwnd
        WindowTitle = [string]$window.title
    }
    if ($Kind -eq 'TextGrab') { $Context.TextGrab = $target } else { $Context.Fixture = $target }
    return $target
}

function Wait-UiTestDiagnosticEvent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$EventName,
        [int]$TimeoutSeconds = 30
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if (Test-Path -LiteralPath $Path) {
            foreach ($line in Get-Content -LiteralPath $Path -ErrorAction SilentlyContinue) {
                try {
                    if (($line | ConvertFrom-Json).eventName -eq $EventName) { return }
                }
                catch { }
            }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out after $TimeoutSeconds seconds waiting for diagnostic event '$EventName' in '$Path'."
}

function Register-UiTest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Suite,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Script,
        [string]$SkipReason,
        [string[]]$RequiredCapabilities = @()
    )

    [void]$script:RegisteredTests.Add([pscustomobject]@{
        Suite = $Suite
        Name = $Name
        Script = $Script
        SkipReason = $SkipReason
        RequiredCapabilities = $RequiredCapabilities
    })
}

function Skip-UiTest {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Reason)

    $exception = [System.InvalidOperationException]::new($Reason)
    $exception.Data['UiTestSkip'] = $Reason
    throw $exception
}

function Import-UiTestSuites {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$SuiteDirectory)

    $script:RegisteredTests.Clear()
    if (-not (Test-Path -LiteralPath $SuiteDirectory)) { return }
    Get-ChildItem -LiteralPath $SuiteDirectory -Filter '*.Tests.ps1' -File |
        Sort-Object Name |
        ForEach-Object { . $_.FullName }
}

function Add-UiTestResult {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Suite,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Outcome,
        [Parameter(Mandatory)][TimeSpan]$Duration,
        [string]$Details,
        [string]$Screenshot
    )

    [void]$Context.Results.Add([pscustomobject]@{
        suite = $Suite
        name = $Name
        outcome = $Outcome
        durationSeconds = [Math]::Round($Duration.TotalSeconds, 3)
        details = $Details
        screenshot = $Screenshot
    })
}

function Invoke-UiTestSuites {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string[]]$Suites
    )

    foreach ($test in @($script:RegisteredTests | Where-Object { $_.Suite -in $Suites })) {
        $started = [DateTimeOffset]::UtcNow
        if (-not [string]::IsNullOrWhiteSpace($test.SkipReason)) {
            Add-UiTestResult -Context $Context -Suite $test.Suite -Name $test.Name -Outcome 'skip' -Duration ([DateTimeOffset]::UtcNow - $started) -Details $test.SkipReason
            continue
        }
        if ($test.RequiredCapabilities.Count -gt 0) {
            $missing = @(Get-UiTestMissingCapabilities -Capabilities $Context.Capabilities.capabilities -RequiredCapabilities $test.RequiredCapabilities)
            if ($missing.Count -gt 0) {
                Add-UiTestResult -Context $Context -Suite $test.Suite -Name $test.Name -Outcome 'skip' -Duration ([DateTimeOffset]::UtcNow - $started) -Details ("Required capability unavailable: " + ($missing -join ' | '))
                continue
            }
        }
        try {
            & $test.Script $Context
            Add-UiTestResult -Context $Context -Suite $test.Suite -Name $test.Name -Outcome 'pass' -Duration ([DateTimeOffset]::UtcNow - $started)
        }
        catch {
            if ($_.Exception.Data.Contains('UiTestSkip')) {
                Add-UiTestResult -Context $Context -Suite $test.Suite -Name $test.Name -Outcome 'skip' -Duration ([DateTimeOffset]::UtcNow - $started) -Details ([string]$_.Exception.Data['UiTestSkip'])
                continue
            }
            $screenshot = $null
            $target = if ($Context.Fixture) { $Context.Fixture } else { $Context.TextGrab }
            if ($target) {
                try { $screenshot = Save-UiTestScreenshot -Context $Context -Target $target -Name "$($test.Suite)-$($test.Name)-failure" -CaptureScreen } catch { }
            }
            Add-UiTestResult -Context $Context -Suite $test.Suite -Name $test.Name -Outcome 'fail' -Duration ([DateTimeOffset]::UtcNow - $started) -Details $_.Exception.ToString() -Screenshot $screenshot
        }
    }
}

function Write-UiTestReports {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Context, [Parameter(Mandatory)]$Environment)

    $failed = @($Context.Results | Where-Object outcome -eq 'fail').Count
    $resultsPath = Join-Path $Context.OutputDirectory 'results.json'
    [ordered]@{
        runId = $Context.RunId
        startedUtc = $Context.StartedUtc
        completedUtc = [DateTimeOffset]::UtcNow
        failed = $failed
        results = @($Context.Results)
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultsPath -Encoding utf8

    $environmentPath = Join-Path $Context.OutputDirectory 'environment.json'
    $Environment | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $environmentPath -Encoding utf8

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $junitPath = Join-Path $Context.OutputDirectory 'junit.xml'
    $writer = [System.Xml.XmlWriter]::Create($junitPath, $settings)
    try {
        $writer.WriteStartDocument()
        $writer.WriteStartElement('testsuite')
        $writer.WriteAttributeString('name', 'TextGrab.UiTests')
        $writer.WriteAttributeString('tests', [string]$Context.Results.Count)
        $writer.WriteAttributeString('failures', [string]$failed)
        $writer.WriteAttributeString('skipped', [string](@($Context.Results | Where-Object outcome -eq 'skip').Count))
        $writer.WriteAttributeString('time', [string]([Math]::Round((@($Context.Results | Measure-Object durationSeconds -Sum).Sum), 3)))
        foreach ($result in $Context.Results) {
            $writer.WriteStartElement('testcase')
            $writer.WriteAttributeString('classname', [string]$result.suite)
            $writer.WriteAttributeString('name', [string]$result.name)
            $writer.WriteAttributeString('time', [string]$result.durationSeconds)
            if ($result.outcome -eq 'skip') {
                $writer.WriteStartElement('skipped')
                $writer.WriteString([string]$result.details)
                $writer.WriteEndElement()
            }
            elseif ($result.outcome -eq 'fail') {
                $writer.WriteStartElement('failure')
                $writer.WriteAttributeString('message', 'UI test failed')
                $writer.WriteString([string]$result.details)
                $writer.WriteEndElement()
            }
            $writer.WriteEndElement()
        }
        $writer.WriteEndElement()
        $writer.WriteEndDocument()
    }
    finally {
        $writer.Dispose()
    }

    [pscustomobject]@{ ResultsPath = $resultsPath; JunitPath = $junitPath; EnvironmentPath = $environmentPath; Failed = $failed }
}

function Stop-UiTestProcesses {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Context)

    foreach ($entry in @($Context.Processes | Sort-Object { $_.Process.Id } -Descending)) {
        $process = $entry.Process
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit(5000) | Out-Null
        }
    }
}

Export-ModuleMember -Function @(
    'New-UiTestRunContext', 'Test-WinAppCli', 'Invoke-UiTestWinApp', 'Get-UiTestWindows',
    'Wait-UiTestWindow', 'New-UiTestWindowTarget', 'Wait-UiTestDialog', 'Set-UiTestForegroundWindow',
    'Invoke-UiTestNativeDialogButton',
    'Wait-UiTestElement', 'Get-UiTestValue', 'Assert-UiTestValue',
    'Get-UiTestProperty', 'Assert-UiTestProperty', 'Set-UiTestValue', 'Invoke-UiTestElement',
    'Click-UiTestElement', 'Focus-UiTestElement', 'Send-UiTestKeys', 'Drag-UiTestElement',
    'Save-UiTestScreenshot', 'Save-UiTestRecording', 'Get-UiTestClipboardSnapshot',
    'Restore-UiTestClipboardSnapshot', 'Assert-UiTestFile', 'Assert-UiTestProcess',
    'Assert-UiTestWindow', 'Get-UiTestDirectoryFingerprint', 'Assert-UiTestUserProfileUnchanged',
    'Start-UiTestProcess', 'Wait-UiTestDiagnosticEvent', 'Register-UiTest',
    'Skip-UiTest', 'Import-UiTestSuites', 'Invoke-UiTestSuites', 'Write-UiTestReports', 'Stop-UiTestProcesses'
)
