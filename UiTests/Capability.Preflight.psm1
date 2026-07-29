Set-StrictMode -Version Latest

function New-UiTestCapability {
    param([bool]$Available, [string]$Reason = '', $Value = $null)

    [pscustomobject]@{
        available = $Available
        reason = $Reason
        value = $Value
    }
}

function Get-UiTestTesseract {
    $candidates = @(@(
        (Join-Path $env:LOCALAPPDATA 'Tesseract-OCR\tesseract.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Tesseract-OCR\tesseract.exe'),
        'C:\Program Files\Tesseract-OCR\tesseract.exe'
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($candidates.Count -eq 0) {
        return New-UiTestCapability $false 'Tesseract was not found in Text Grab supported install locations.'
    }

    $path = $candidates[0]
    $output = & $path --list-langs 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        return New-UiTestCapability $false "Tesseract at '$path' could not list its languages: $output"
    }

    $languages = @($output -split '\r?\n' | Where-Object {
        $_ -and $_ -notmatch '^\s*(List of available languages|osd)'
    } | ForEach-Object { $_.Trim() })
    New-UiTestCapability $true '' ([pscustomobject]@{ path = $path; languages = $languages })
}

function Get-UiTestWinRtOcrLanguages {
    try {
        $languages = @([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages |
            ForEach-Object { $_.LanguageTag } | Sort-Object -Unique)
        return New-UiTestCapability ($languages.Count -gt 0) $(if ($languages.Count -eq 0) { 'No Windows OCR language packs are installed.' }) $languages
    }
    catch {
        try {
            $languages = @(Get-WindowsCapability -Online -ErrorAction Stop |
                Where-Object { $_.Name -like 'Language.OCR*' -and $_.State -eq 'Installed' } |
                ForEach-Object { (($_.Name -split '~')[1] -split '\.')[0] } | Sort-Object -Unique)
            return New-UiTestCapability ($languages.Count -gt 0) $(if ($languages.Count -eq 0) { "No Windows OCR language packs are installed. WinRT enumeration also failed: $($_.Exception.Message)" }) $languages
        }
        catch {
            return New-UiTestCapability $false "Windows OCR language enumeration failed: $($_.Exception.Message)"
        }
    }
}

function Get-UiTestTtsVoices {
    try {
        $voices = @([Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices |
            ForEach-Object { [pscustomobject]@{ displayName = $_.DisplayName; language = $_.Language; gender = $_.Gender.ToString() } })
        return New-UiTestCapability ($voices.Count -gt 0) $(if ($voices.Count -eq 0) { 'No Windows speech synthesis voices are installed.' }) $voices
    }
    catch {
        $voiceKeys = @(
            'HKLM:\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens',
            'HKLM:\SOFTWARE\Microsoft\Speech\Voices\Tokens'
        )
        $voices = @($voiceKeys | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object {
            Get-ChildItem -LiteralPath $_ | ForEach-Object {
                $attributes = Get-ItemProperty -LiteralPath (Join-Path $_.PSPath 'Attributes') -ErrorAction SilentlyContinue
                [pscustomobject]@{
                    displayName = $_.GetValue('')
                    language = $attributes.Language
                    gender = $attributes.Gender
                }
            }
        })
        return New-UiTestCapability ($voices.Count -gt 0) $(if ($voices.Count -eq 0) { "Windows speech voice enumeration failed: $($_.Exception.Message)" }) $voices
    }
}

function Get-UiTestMonitors {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public struct UiTestPoint { public int X; public int Y; }
public static class UiTestMonitorNative {
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(UiTestPoint point, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
}
'@ -ErrorAction SilentlyContinue
    $screens = @([System.Windows.Forms.Screen]::AllScreens | ForEach-Object {
        $dpi = 96
        try {
            [uint32]$x = 96
            [uint32]$y = 96
            $previousContext = [UiTestMonitorNative]::SetThreadDpiAwarenessContext([IntPtr](-4))
            $point = [UiTestPoint]@{ X = $_.Bounds.X + 1; Y = $_.Bounds.Y + 1 }
            $handle = [UiTestMonitorNative]::MonitorFromPoint($point, 2)
            if ($handle -ne [IntPtr]::Zero -and [UiTestMonitorNative]::GetDpiForMonitor($handle, 0, [ref]$x, [ref]$y) -eq 0) {
                $dpi = [int]$x
            }
            if ($previousContext -ne [IntPtr]::Zero) {
                [void][UiTestMonitorNative]::SetThreadDpiAwarenessContext($previousContext)
            }
        }
        catch { }

        [pscustomobject]@{
            deviceName = $_.DeviceName
            primary = $_.Primary
            bounds = [pscustomobject]@{ x = $_.Bounds.X; y = $_.Bounds.Y; width = $_.Bounds.Width; height = $_.Bounds.Height }
            workingArea = [pscustomobject]@{ x = $_.WorkingArea.X; y = $_.WorkingArea.Y; width = $_.WorkingArea.Width; height = $_.WorkingArea.Height }
            dpi = $dpi
            scalingPercent = [math]::Round($dpi / 96 * 100)
        }
    })
    return $screens
}

function Get-UiTestHdrState {
    try {
        $instances = @(Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorAdvancedColorCapabilities -ErrorAction Stop)
        $enabled = @($instances | Where-Object { $_.AdvancedColorSupported -or $_.AdvancedColorEnabled })
        return New-UiTestCapability ($enabled.Count -gt 0) $(if ($enabled.Count -eq 0) { 'No monitor reports advanced-color/HDR support.' }) @($instances | ForEach-Object {
            [pscustomobject]@{
                instanceName = $_.InstanceName
                supported = [bool]$_.AdvancedColorSupported
                enabled = [bool]$_.AdvancedColorEnabled
            }
        })
    }
    catch {
        return New-UiTestCapability $false "HDR capability query is unavailable: $($_.Exception.Message)"
    }
}

function Get-UiTestExecutableArchitecture {
    param([Parameter(Mandatory)][string]$Path)

    try {
        $stream = [IO.File]::OpenRead($Path)
        try {
            $reader = [IO.BinaryReader]::new($stream)
            $stream.Position = 0x3c
            $headerOffset = $reader.ReadInt32()
            $stream.Position = $headerOffset + 4
            switch ($reader.ReadUInt16()) {
                0x8664 { return 'x64' }
                0xaa64 { return 'ARM64' }
                0x014c { return 'x86' }
                default { return 'unknown' }
            }
        }
        finally { $stream.Dispose() }
    }
    catch { return "unreadable: $($_.Exception.Message)" }
}

function Get-UiTestCapabilityManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TextGrabExecutable,
        [Parameter(Mandatory)][string]$PackageManifestPath
    )

    $os = Get-CimInstance Win32_OperatingSystem
    $computer = Get-CimInstance Win32_ComputerSystem
    $version = [Environment]::OSVersion.Version
    $isWindows11 = $version.Build -ge 22000
    $isArm64 = [Environment]::Is64BitOperatingSystem -and $env:PROCESSOR_ARCHITECTURE -match 'ARM64'
    $monitors = @(Get-UiTestMonitors)
    $tesseract = Get-UiTestTesseract
    $ocrLanguages = Get-UiTestWinRtOcrLanguages
    $voices = Get-UiTestTtsVoices
    $hdr = Get-UiTestHdrState
    # AppxPackage exposes recursively nested dependency and capability objects.
    # Keep only the values the lanes consume so environment.json is complete at
    # the report serializer's depth and never emits a truncation warning.
    $package = @(Get-AppxPackage -Name '40087JoeFinApps.TextGrab' -ErrorAction SilentlyContinue |
        Select-Object -First 1 |
        ForEach-Object {
            [pscustomobject]@{
                name = $_.Name
                packageFullName = $_.PackageFullName
                packageFamilyName = $_.PackageFamilyName
                version = $_.Version.ToString()
                architecture = $_.Architecture.ToString()
                installLocation = $_.InstallLocation
                isDevelopmentMode = [bool]$_.IsDevelopmentMode
                status = $_.Status.ToString()
            }
        })
    $manifestText = if (Test-Path -LiteralPath $PackageManifestPath) { Get-Content -LiteralPath $PackageManifestPath -Raw } else { '' }
    $highContrast = $false
    try { $highContrast = [SystemParameters]::HighContrast } catch { }
    $appTheme = 'system'
    try {
        $themeValue = (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name AppsUseLightTheme -ErrorAction Stop).AppsUseLightTheme
        $appTheme = if ($themeValue -eq 0) { 'dark' } else { 'light' }
    }
    catch { }
    $notificationsDisabled = $false
    try {
        $notificationsDisabled = (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications' -Name ToastEnabled -ErrorAction Stop).ToastEnabled -eq 0
    }
    catch { }

    $winAiReason = if (-not $isWindows11) { 'Windows AI is unavailable on Windows 10.' }
        elseif (-not $isArm64) { 'Windows AI is restricted to ARM64 unless the app debug override is explicitly enabled.' }
        elseif ($package.Count -eq 0) { 'Windows AI requires the packaged app; no Text Grab package is registered.' }
        else { '' }

    [ordered]@{
        collectedUtc = [DateTimeOffset]::UtcNow
        os = [ordered]@{
            caption = $os.Caption
            version = $os.Version
            build = $version.Build
            windows11 = $isWindows11
            windows10 = -not $isWindows11
            architecture = $computer.SystemType
        }
        processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        appArchitecture = Get-UiTestExecutableArchitecture -Path $TextGrabExecutable
        appVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($TextGrabExecutable).FileVersion
        capabilities = [ordered]@{
            windows = New-UiTestCapability $true '' $version.Build
            windows10 = New-UiTestCapability (-not $isWindows11) $(if ($isWindows11) { 'This lane is specifically for Windows 10 behavior.' })
            windows11 = New-UiTestCapability $isWindows11 $(if (-not $isWindows11) { "Windows build $($version.Build) is earlier than Windows 11." })
            x64 = New-UiTestCapability ($env:PROCESSOR_ARCHITECTURE -match 'AMD64') $(if ($env:PROCESSOR_ARCHITECTURE -notmatch 'AMD64') { "Current architecture is $env:PROCESSOR_ARCHITECTURE." })
            arm64 = New-UiTestCapability $isArm64 $(if (-not $isArm64) { "Current architecture is $env:PROCESSOR_ARCHITECTURE." })
            interactiveDesktop = New-UiTestCapability ([Environment]::UserInteractive) $(if (-not [Environment]::UserInteractive) { 'The process is not attached to an interactive Windows desktop.' })
            disposableVm = New-UiTestCapability ($env:TEXT_GRAB_DISPOSABLE_VM -eq '1') $(if ($env:TEXT_GRAB_DISPOSABLE_VM -ne '1') { 'Destructive packaged scenarios require TEXT_GRAB_DISPOSABLE_VM=1 on a resettable VM.' })
            multiMonitor = New-UiTestCapability ($monitors.Count -gt 1) $(if ($monitors.Count -le 1) { 'Mixed-monitor validation requires at least two monitors.' }) $monitors
            mixedDpi = New-UiTestCapability ((@($monitors.scalingPercent | Select-Object -Unique).Count) -gt 1) $(if ((@($monitors.scalingPercent | Select-Object -Unique).Count) -le 1) { 'Mixed-DPI validation requires monitors with different scaling percentages.' }) $monitors
            non100Dpi = New-UiTestCapability (@($monitors | Where-Object scalingPercent -ne 100).Count -gt 0) $(if (@($monitors | Where-Object scalingPercent -ne 100).Count -eq 0) { 'No monitor is configured above or below 100% scaling.' }) $monitors
            highContrast = New-UiTestCapability $highContrast $(if (-not $highContrast) { 'Windows High Contrast is not enabled.' })
            hdr = $hdr
            tesseract = $tesseract
            winrtOcr = $ocrLanguages
            cjkOcr = New-UiTestCapability (@($ocrLanguages.value | Where-Object { $_ -match '^(ja|zh|ko)' }).Count -gt 0) $(if (@($ocrLanguages.value | Where-Object { $_ -match '^(ja|zh|ko)' }).Count -eq 0) { 'No Japanese, Chinese, or Korean Windows OCR language pack is installed.' }) $ocrLanguages.value
            tts = $voices
            notifications = New-UiTestCapability (-not $notificationsDisabled) $(if ($notificationsDisabled) { 'Windows notifications are disabled for the current user.' })
            packaged = New-UiTestCapability ($package.Count -gt 0) $(if ($package.Count -eq 0) { 'No registered Text Grab MSIX package was found.' }) $package
            packageSupport = New-UiTestCapability ($manifestText -match 'windows.protocol' -and $manifestText -match 'windows.shareTarget' -and $manifestText -match 'windows.startupTask') $(if (-not ($manifestText -match 'windows.protocol' -and $manifestText -match 'windows.shareTarget' -and $manifestText -match 'windows.startupTask')) { if ([string]::IsNullOrWhiteSpace($manifestText)) { "Package manifest not found: $PackageManifestPath" } else { 'The package manifest is missing one or more required protocol, share-target, or startup declarations.' } })
            shareTarget = New-UiTestCapability ($package.Count -gt 0 -and $manifestText -match 'windows.shareTarget') $(if ($package.Count -eq 0) { 'Share-target activation is only testable from a registered package.' } elseif ($manifestText -notmatch 'windows.shareTarget') { 'The package manifest does not declare a share target.' })
            windowsAi = New-UiTestCapability ([string]::IsNullOrEmpty($winAiReason)) $winAiReason
        }
        display = [ordered]@{ theme = $appTheme; highContrast = $highContrast; monitors = $monitors }
        ocr = [ordered]@{ winRtLanguages = $ocrLanguages.value; tesseract = $tesseract.value }
        audio = [ordered]@{ voices = $voices.value }
        packageManifestVersion = if ($manifestText -match '<Identity[^>]*Version="([^"]+)"') { $matches[1] } else { $null }
    }
}

function Get-UiTestMissingCapabilities {
    param([Parameter(Mandatory)]$Capabilities, [string[]]$RequiredCapabilities)

    @($RequiredCapabilities | ForEach-Object {
        $entry = $Capabilities.$_
        if ($null -eq $entry) { return "Unknown capability declaration '$_'." }
        if (-not $entry.available) { return "${_}: $($entry.reason)" }
    } | Where-Object { $_ })
}

Export-ModuleMember -Function @(
    'Get-UiTestCapabilityManifest',
    'Get-UiTestMissingCapabilities'
)
