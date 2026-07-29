[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [string[]]$RequiredCapability = @(),
    [switch]$RequireDisplay,
    [switch]$RequireMixedDpi,
    [switch]$Destructive,
    [ValidateRange(1, 500)][int]$MinimumFreeDiskGB = 25
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-PreflightCheck {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[object]]$Checks,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][bool]$Passed,
        [Parameter(Mandatory)][string]$Details
    )

    [void]$Checks.Add([ordered]@{
        name = $Name
        passed = $Passed
        details = $Details
    })
}

function Get-InputDesktopState {
    if ($null -eq ('TextGrab.InteractiveCi.Desktop' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace TextGrab.InteractiveCi
{
    public static class Desktop
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseDesktop(IntPtr desktop);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool GetUserObjectInformation(
            IntPtr handle, int index, IntPtr value, int length, out int needed);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}
'@
    }

    $desktop = [TextGrab.InteractiveCi.Desktop]::OpenInputDesktop(0, $false, 0x0100)
    if ($desktop -eq [IntPtr]::Zero) {
        return [ordered]@{
            available = $false
            name = $null
            error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
            foregroundWindow = [TextGrab.InteractiveCi.Desktop]::GetForegroundWindow().ToInt64()
        }
    }

    try {
        $needed = 0
        [void][TextGrab.InteractiveCi.Desktop]::GetUserObjectInformation($desktop, 2, [IntPtr]::Zero, 0, [ref]$needed)
        $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($needed)
        try {
            $success = [TextGrab.InteractiveCi.Desktop]::GetUserObjectInformation($desktop, 2, $buffer, $needed, [ref]$needed)
            [ordered]@{
                available = $success
                name = if ($success) { [Runtime.InteropServices.Marshal]::PtrToStringUni($buffer) } else { $null }
                error = if ($success) { $null } else { [Runtime.InteropServices.Marshal]::GetLastWin32Error() }
                foregroundWindow = [TextGrab.InteractiveCi.Desktop]::GetForegroundWindow().ToInt64()
            }
        }
        finally {
            [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
        }
    }
    finally {
        [void][TextGrab.InteractiveCi.Desktop]::CloseDesktop($desktop)
    }
}

function Get-MonitorState {
    if ($null -eq ('TextGrab.InteractiveCi.Monitors' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TextGrab.InteractiveCi
{
    public static class Monitors
    {
        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        public static uint[] GetDpis()
        {
            var dpis = new List<uint>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, hdc, rect, data) =>
            {
                uint dpiX, dpiY;
                if (GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0)
                    dpis.Add(dpiX);
                return true;
            }, IntPtr.Zero);
            return dpis.ToArray();
        }

        public static int Count { get { return GetSystemMetrics(80); } }
    }
}
'@
    }

    $dpis = @([TextGrab.InteractiveCi.Monitors]::GetDpis())
    [ordered]@{
        count = [TextGrab.InteractiveCi.Monitors]::Count
        dpis = $dpis
        mixedDpi = (@($dpis | Select-Object -Unique).Count -gt 1)
    }
}

$checks = [System.Collections.Generic.List[object]]::new()
$manifest = [ordered]@{
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    requiredCapabilities = @($RequiredCapability | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    destructive = [bool]$Destructive
    checks = $checks
    environment = [ordered]@{}
}

try {
    $isWindowsHost = [OperatingSystem]::IsWindows()
    Add-PreflightCheck -Checks $checks -Name 'windows' -Passed $isWindowsHost -Details "Operating system: $([Environment]::OSVersion.VersionString)"
    if (-not $isWindowsHost) { throw 'Interactive UI testing requires Windows.' }

    $manifest.environment.computerName = $env:COMPUTERNAME
    $manifest.environment.userName = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $manifest.environment.processId = $PID
    $process = Get-Process -Id $PID
    $manifest.environment.sessionId = $process.SessionId
    $manifest.environment.userInteractive = [Environment]::UserInteractive
    $manifest.environment.osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    $manifest.environment.processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()

    $interactiveSession = [Environment]::UserInteractive -and $process.SessionId -ne 0
    Add-PreflightCheck -Checks $checks -Name 'interactive-user-session' -Passed $interactiveSession -Details "userInteractive=$([Environment]::UserInteractive); sessionId=$($process.SessionId); user=$($manifest.environment.userName)"

    $desktop = Get-InputDesktopState
    $manifest.environment.inputDesktop = $desktop
    $unlockedDesktop = $desktop.available -and $desktop.name -eq 'Default'
    Add-PreflightCheck -Checks $checks -Name 'unlocked-input-desktop' -Passed $unlockedDesktop -Details "available=$($desktop.available); desktop=$($desktop.name); error=$($desktop.error); foregroundWindow=$($desktop.foregroundWindow)"

    $dotnetVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    $dotnetAvailable = $LASTEXITCODE -eq 0 -and $dotnetVersion -match '^10\.'
    $manifest.environment.dotnetSdk = $dotnetVersion
    Add-PreflightCheck -Checks $checks -Name 'dotnet-sdk-10' -Passed $dotnetAvailable -Details "dotnet --version: $dotnetVersion"

    $winapp = Get-Command winapp -ErrorAction SilentlyContinue
    $winappVersion = if ($null -ne $winapp) { (& winapp --version 2>&1 | Out-String).Trim() } else { '' }
    $winappAvailable = $null -ne $winapp -and $LASTEXITCODE -eq 0 -and $winappVersion -match '^0\.5\.\d+(?:[-+].*)?$'
    $manifest.environment.winapp = [ordered]@{ path = if ($winapp) { $winapp.Source } else { $null }; version = $winappVersion }
    Add-PreflightCheck -Checks $checks -Name 'winapp-cli-0.5' -Passed $winappAvailable -Details "path=$($manifest.environment.winapp.path); version=$winappVersion"

    $monitors = Get-MonitorState
    $manifest.environment.monitors = $monitors
    if ($RequireDisplay) {
        Add-PreflightCheck -Checks $checks -Name 'display' -Passed ($monitors.count -ge 1) -Details "monitorCount=$($monitors.count); dpis=$($monitors.dpis -join ',')"
    }
    if ($RequireMixedDpi) {
        Add-PreflightCheck -Checks $checks -Name 'mixed-dpi-display' -Passed ($monitors.count -ge 2 -and $monitors.mixedDpi) -Details "monitorCount=$($monitors.count); dpis=$($monitors.dpis -join ',')"
    }

    $driveRoot = [IO.Path]::GetPathRoot([IO.Path]::GetFullPath((Get-Location).Path))
    $drive = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$($driveRoot.TrimEnd('\'))'" | Select-Object -First 1
    $freeDiskGb = if ($drive) { [Math]::Round($drive.FreeSpace / 1GB, 2) } else { 0 }
    $manifest.environment.freeDiskGb = $freeDiskGb
    Add-PreflightCheck -Checks $checks -Name 'free-disk' -Passed ($freeDiskGb -ge $MinimumFreeDiskGB) -Details "freeDiskGb=$freeDiskGb; requiredGb=$MinimumFreeDiskGB; drive=$driveRoot"

    $configuredCapabilities = @(
        $env:TEXT_GRAB_INTERACTIVE_CAPABILITIES -split '[,; ]+' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim().ToLowerInvariant() }
    )
    $automaticCapabilities = @()
    if ($monitors.count -ge 1) { $automaticCapabilities += 'display' }
    if ($monitors.count -ge 2 -and $monitors.mixedDpi) { $automaticCapabilities += 'mixed-dpi' }
    if ($manifest.environment.osArchitecture -eq 'Arm64') { $automaticCapabilities += 'arm64' }
    $availableCapabilities = @($configuredCapabilities + $automaticCapabilities | Select-Object -Unique)
    $manifest.environment.capabilities = [ordered]@{
        configured = $configuredCapabilities
        automatic = $automaticCapabilities
        available = $availableCapabilities
    }

    foreach ($capability in @($manifest.requiredCapabilities | ForEach-Object { $_.Trim().ToLowerInvariant() })) {
        Add-PreflightCheck -Checks $checks -Name "capability:$capability" -Passed ($capability -in $availableCapabilities) -Details "available=$($availableCapabilities -join ',')"
    }

    if ($Destructive) {
        $collisions = @(Get-Process -Name 'Text-Grab' -ErrorAction SilentlyContinue | ForEach-Object {
            [ordered]@{ id = $_.Id; path = $_.Path }
        })
        $manifest.environment.textGrabCollisions = $collisions
        Add-PreflightCheck -Checks $checks -Name 'no-text-grab-collision' -Passed ($collisions.Count -eq 0) -Details "runningTextGrabProcesses=$($collisions.Count)"
    }
}
catch {
    Add-PreflightCheck -Checks $checks -Name 'preflight-execution' -Passed $false -Details $_.Exception.ToString()
}
finally {
    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }

    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding utf8
}

$failed = @($checks | Where-Object { -not $_.passed })
if ($failed.Count -gt 0) {
    throw "Interactive CI preflight failed: $($failed.name -join ', '). See $OutputPath."
}
