[CmdletBinding()]
param(
    [string]$TextGrabPath = (Join-Path $PSScriptRoot '..\Text-Grab\Text-Grab.csproj'),
    [string]$FixtureHostPath = (Join-Path $PSScriptRoot 'TextGrab.AutomationHost\TextGrab.AutomationHost.csproj'),
    [string[]]$Suite = @('Harness'),
    [string]$ArtifactRoot = (Join-Path $PSScriptRoot 'artifacts'),
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$Runtime,
    [switch]$NoBuild,
    [switch]$SystemIntegration,
    [switch]$DisposableRegistration,
    [switch]$Record,
    [string[]]$RequiredCapability = @(),
    [ValidateRange(5, 300)][int]$TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$suiteAliases = @{
    Display = 'DisplayHardware'
    Ocr = 'OcrCapability'
    Architecture = 'OsArchitecture'
    NotificationTtsShare = 'NotificationsTtsShare'
}
$Suite = @($Suite | ForEach-Object { $_ -split ',' } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object {
        $name = $_.Trim()
        if ($suiteAliases.ContainsKey($name)) { $suiteAliases[$name] } else { $name }
    })
Import-Module (Join-Path $PSScriptRoot 'TextGrab.UiTestHarness.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $PSScriptRoot 'Capability.Preflight.psm1') -Force -DisableNameChecking

function Resolve-UiTestExecutable {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Name)

    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)) {
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "$Name executable was not found: $fullPath" }
        return $fullPath
    }
    if (-not $fullPath.EndsWith('.csproj', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name path must be an .exe or .csproj: $fullPath"
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "$Name project was not found: $fullPath" }
    if (-not $NoBuild) {
        $buildArguments = @('build', $fullPath, '-c', $Configuration, '--nologo')
        if ($Runtime) { $buildArguments += @('-r', $Runtime) }
        & dotnet @buildArguments | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Building $Name failed with exit code $LASTEXITCODE." }
    }
    $projectDirectory = Split-Path -Parent $fullPath
    $outputDirectory = Join-Path $projectDirectory "bin\$Configuration"
    if ($Runtime) { $outputDirectory = Join-Path $outputDirectory $Runtime }
    $candidates = @(Get-ChildItem -Path $outputDirectory -Filter "$Name.exe" -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\win-(?:x86|x64|arm64)\\msixpublish\\' } |
        Sort-Object LastWriteTimeUtc -Descending)
    if ($candidates.Count -eq 0) { throw "No built $Name executable was found under $outputDirectory. Run without -NoBuild or supply its .exe path." }
    return $candidates[0].FullName
}

$context = $null
$clipboard = $null
$allowsDisposableRegistration = $DisposableRegistration -and $SystemIntegration -and $env:TEXT_GRAB_DISPOSABLE_VM -eq '1'
if ($DisposableRegistration -and -not $allowsDisposableRegistration) {
    throw '-DisposableRegistration requires -SystemIntegration and TEXT_GRAB_DISPOSABLE_VM=1 on a resettable VM.'
}
$environment = [ordered]@{
    computerName = $env:COMPUTERNAME
    userName = $env:USERNAME
    os = [Environment]::OSVersion.VersionString
    powershell = $PSVersionTable.PSVersion.ToString()
    configuration = $Configuration
    runtime = $Runtime
    systemIntegration = [bool]$SystemIntegration
    disposableRegistration = [bool]$allowsDisposableRegistration
}

try {
    $context = New-UiTestRunContext -ArtifactRoot $ArtifactRoot -SystemIntegration:$SystemIntegration -Record:$Record -TimeoutSeconds $TimeoutSeconds
    $winapp = Test-WinAppCli
    $environment.winapp = $winapp
    $textGrabExe = Resolve-UiTestExecutable -Path $TextGrabPath -Name 'Text-Grab'
    $fixtureExe = Resolve-UiTestExecutable -Path $FixtureHostPath -Name 'TextGrab.AutomationHost'
    $environment.textGrabExe = $textGrabExe
    $environment.fixtureExe = $fixtureExe
    $context.Capabilities = Get-UiTestCapabilityManifest -TextGrabExecutable $textGrabExe -PackageManifestPath (Join-Path $PSScriptRoot '..\Text-Grab-Package\Package.appxmanifest')
    $environment.capabilities = $context.Capabilities
    $missingCapabilities = @(Get-UiTestMissingCapabilities -Capabilities $context.Capabilities.capabilities -RequiredCapabilities $RequiredCapability)
    if ($missingCapabilities.Count -gt 0) {
        throw "Required CI capability unavailable: $($missingCapabilities -join ' | ')"
    }

    $clipboard = Get-UiTestClipboardSnapshot
    $environment.profileDirectory = $context.ProfileDirectory
    $environment.seedPath = $context.SeedPath
    $context.UserProfileRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'Text-Grab'
    $context.UserProfileFingerprint = Get-UiTestDirectoryFingerprint -Path $context.UserProfileRoot
    $environment.diagnosticsPath = Join-Path $context.ProfileDirectory 'diagnostics\events.jsonl'
    $environment.fixtureStatePath = $context.FixtureStatePath

    $fixture = Start-UiTestProcess -Context $context -FilePath $fixtureExe -Arguments @('--state-file', $context.FixtureStatePath) -Kind Fixture -WindowTitle 'Text Grab Automation Fixture Host'
    Wait-UiTestElement -Target $fixture -AutomationId 'FixtureHostTitle' -TimeoutSeconds $TimeoutSeconds
    $textArguments = @('--automation-profile', $context.ProfileDirectory, 'EditText')
    if ($SystemIntegration) { $textArguments += '--automation-system-integration' }
    if ($allowsDisposableRegistration) { $textArguments += '--automation-disposable-registration' }
    $textGrab = Start-UiTestProcess -Context $context -FilePath $textGrabExe -Arguments $textArguments -Kind TextGrab
    Wait-UiTestDiagnosticEvent -Path (Join-Path $context.ProfileDirectory 'diagnostics\events.jsonl') -EventName 'ready' -TimeoutSeconds $TimeoutSeconds
    $environment.fixturePid = $fixture.ProcessId
    $environment.fixtureHwnd = $fixture.WindowHandle
    $environment.textGrabPid = $textGrab.ProcessId
    $environment.textGrabHwnd = $textGrab.WindowHandle

    Import-UiTestSuites -SuiteDirectory (Join-Path $PSScriptRoot 'Suites')
    Invoke-UiTestSuites -Context $context -Suites $Suite
    if ($context.Results.Count -eq 0) { throw "No tests registered for requested suite(s): $($Suite -join ', ')." }
}
catch {
    if ($null -ne $context) {
        $context.Results.Add([pscustomobject]@{
            suite = 'Harness'
            name = 'Runner setup'
            outcome = 'fail'
            durationSeconds = 0
            details = $_.Exception.ToString() + "`r`n" + $_.ScriptStackTrace
            screenshot = $null
        })
    }
    else {
        throw
    }
}
finally {
    if ($null -ne $context) {
        if ($null -ne $clipboard) {
            try { Restore-UiTestClipboardSnapshot -Snapshot $clipboard } catch { }
        }
        Stop-UiTestProcesses -Context $context
        $report = Write-UiTestReports -Context $context -Environment $environment
        Write-Output "Results: $($report.ResultsPath)"
        Write-Output "JUnit: $($report.JunitPath)"
        if ($report.Failed -gt 0) { exit 1 }
    }
}
