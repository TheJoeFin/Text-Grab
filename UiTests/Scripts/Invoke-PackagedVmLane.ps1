[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [string]$PreviousPackagePath,
    [string]$FixtureDirectory = (Join-Path $PSScriptRoot '..\..\Tests\Images'),
    [string]$TggfFixturePath,
    [switch]$RunDestructiveLifecycle,
    [switch]$ConfirmDisposableVm,
    [ValidateRange(5, 120)][int]$TimeoutSeconds = 30,
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot '..\artifacts\packaged-vm')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Results = [System.Collections.Generic.List[object]]::new()
$script:HasFailures = $false

function Add-PackagedVmResult {
    param(
        [Parameter(Mandatory)][string]$Scenario,
        [Parameter(Mandatory)][ValidateSet('pass', 'fail', 'skip')][string]$Outcome,
        [Parameter(Mandatory)][string]$Reason,
        $Details = $null
    )

    if ($Outcome -eq 'fail') {
        $script:HasFailures = $true
    }

    [void]$script:Results.Add([pscustomobject]@{
        scenario = $Scenario
        outcome = $Outcome
        reason = $Reason
        details = $Details
    })
}

function Write-PackagedVmResults {
    New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null
    [ordered]@{
        collectedUtc = [DateTimeOffset]::UtcNow
        packagePath = [IO.Path]::GetFullPath($PackagePath)
        destructiveLifecycle = [bool]$RunDestructiveLifecycle
        results = @($script:Results)
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'packaged-vm-results.json') -Encoding utf8
}

function Invoke-PackagedScenario {
    param(
        [Parameter(Mandatory)][string]$Scenario,
        [Parameter(Mandatory)][string]$SuccessReason,
        [Parameter(Mandatory)][scriptblock]$Script
    )

    try {
        $details = & $Script
        Add-PackagedVmResult -Scenario $Scenario -Outcome pass -Reason $SuccessReason -Details $details
        return $true
    }
    catch {
        Add-PackagedVmResult -Scenario $Scenario -Outcome fail -Reason $_.Exception.Message -Details @{ exception = $_.Exception.ToString() }
        return $false
    }
}

function Read-PackageZipEntryText {
    param([Parameter(Mandatory)]$Entry)

    $reader = [IO.StreamReader]::new($Entry.Open())
    try {
        return $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}

function Get-PackageArtifactMetadata {
    param([Parameter(Mandatory)][string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $appManifestEntry = $archive.Entries | Where-Object { $_.FullName -ieq 'AppxManifest.xml' } | Select-Object -First 1
        if ($appManifestEntry) {
            return [pscustomobject]@{
                artifactType = 'package'
                manifest = Read-PackageZipEntryText -Entry $appManifestEntry
                selectedPayload = $null
                supportedArchitectures = @()
            }
        }

        $bundleManifestEntry = $archive.Entries | Where-Object { $_.FullName -ieq 'AppxMetadata/AppxBundleManifest.xml' } | Select-Object -First 1
        if ($null -eq $bundleManifestEntry) {
            throw "Package artifact '$Path' contains neither AppxManifest.xml nor AppxMetadata/AppxBundleManifest.xml."
        }

        [xml]$bundleManifest = Read-PackageZipEntryText -Entry $bundleManifestEntry
        $packageNodes = @($bundleManifest.SelectNodes("//*[local-name()='Package']") |
            Where-Object {
                -not [string]::IsNullOrWhiteSpace($_.GetAttribute('FileName')) -and
                -not [string]::IsNullOrWhiteSpace($_.GetAttribute('Architecture'))
            })
        if ($packageNodes.Count -eq 0) {
            throw "Bundle '$Path' declares no inner package payloads."
        }

        $architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
        $selectedNode = $packageNodes |
            Where-Object { $_.GetAttribute('Architecture').ToLowerInvariant() -eq $architecture } |
            Select-Object -First 1
        if ($null -eq $selectedNode) {
            $selectedNode = $packageNodes |
                Where-Object { $_.GetAttribute('Architecture').ToLowerInvariant() -eq 'neutral' } |
                Select-Object -First 1
        }
        if ($null -eq $selectedNode) {
            $available = @($packageNodes | ForEach-Object { $_.GetAttribute('Architecture') }) -join ', '
            throw "Bundle '$Path' has no $architecture or neutral payload. Available architectures: $available."
        }

        $payloadName = $selectedNode.GetAttribute('FileName')
        $payloadEntry = $archive.Entries | Where-Object {
            $_.FullName -ieq $payloadName -or $_.FullName.EndsWith("/$payloadName", [StringComparison]::OrdinalIgnoreCase)
        } | Select-Object -First 1
        if ($null -eq $payloadEntry) {
            throw "Bundle '$Path' declares payload '$payloadName', but that payload is absent."
        }

        $payloadStream = $payloadEntry.Open()
        $payloadBytes = [IO.MemoryStream]::new()
        try {
            $payloadStream.CopyTo($payloadBytes)
            $payloadBytes.Position = 0
            $innerArchive = [IO.Compression.ZipArchive]::new($payloadBytes, [IO.Compression.ZipArchiveMode]::Read, $true)
            try {
                $innerManifestEntry = $innerArchive.Entries | Where-Object { $_.FullName -ieq 'AppxManifest.xml' } | Select-Object -First 1
                if ($null -eq $innerManifestEntry) {
                    throw "Bundle payload '$payloadName' does not contain AppxManifest.xml."
                }
                $manifest = Read-PackageZipEntryText -Entry $innerManifestEntry
            }
            finally {
                $innerArchive.Dispose()
            }
        }
        finally {
            $payloadStream.Dispose()
            $payloadBytes.Dispose()
        }

        return [pscustomobject]@{
            artifactType = 'bundle'
            manifest = $manifest
            selectedPayload = [pscustomobject]@{
                fileName = $payloadName
                architecture = $selectedNode.GetAttribute('Architecture')
                version = $selectedNode.GetAttribute('Version')
            }
            supportedArchitectures = @($packageNodes | ForEach-Object { $_.GetAttribute('Architecture') } | Sort-Object -Unique)
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-ManifestContains {
    param([Parameter(Mandatory)][string]$Manifest, [Parameter(Mandatory)][string]$Expected)

    if ($Manifest -notlike "*$Expected*") {
        throw "Installed package artifact manifest does not declare '$Expected'."
    }
}

function Assert-PackageArtifactSignature {
    param([Parameter(Mandatory)][string]$Path)

    $signature = Get-AuthenticodeSignature -FilePath $Path
    if ($signature.Status -ne 'Valid') {
        throw "Package signature verification failed for '$Path': $($signature.Status) $($signature.StatusMessage)"
    }
}

function Stop-TextGrabUnderTest {
    foreach ($process in @(Get-Process -Name 'Text-Grab' -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $process.Id -Force -ErrorAction Stop
        $process.WaitForExit(5000) | Out-Null
    }
}

function Wait-TextGrabWindow {
    param(
        [string]$ExpectedTitle = '*',
        [Parameter(Mandatory)][datetime]$StartedAfter
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $candidate = @(Get-Process -Name 'Text-Grab' -ErrorAction SilentlyContinue |
            Where-Object {
                $_.StartTime.ToUniversalTime() -ge $StartedAfter.AddSeconds(-2) -and
                $_.MainWindowHandle -ne 0 -and
                $_.MainWindowTitle -like $ExpectedTitle
            } |
            Select-Object -First 1)[0]
        if ($candidate) {
            return [pscustomobject]@{
                processId = $candidate.Id
                windowHandle = $candidate.MainWindowHandle.ToInt64()
                windowTitle = $candidate.MainWindowTitle
            }
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out after $TimeoutSeconds seconds waiting for Text Grab window '$ExpectedTitle'."
}

function Invoke-AssociationVerb {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Verb,
        [Parameter(Mandatory)][string]$ExpectedTitle
    )

    Stop-TextGrabUnderTest
    $started = [DateTime]::UtcNow
    Start-Process -FilePath $Path -Verb $Verb -ErrorAction Stop
    return Wait-TextGrabWindow -ExpectedTitle $ExpectedTitle -StartedAfter $started
}

function Get-RegisteredTextGrabPackage {
    $package = @(Get-AppxPackage -Name '40087JoeFinApps.TextGrab' -ErrorAction SilentlyContinue | Select-Object -First 1)[0]
    if ($null -eq $package) {
        throw 'Text Grab was not registered after package installation.'
    }
    return $package
}

function Get-InstalledTextGrabManifest {
    param([Parameter(Mandatory)]$Package)

    $manifest = Get-AppxPackageManifest -Package $Package.PackageFullName -ErrorAction Stop
    if ($manifest.Package.Identity.Name -ne '40087JoeFinApps.TextGrab') {
        throw "Installed package identity was '$($manifest.Package.Identity.Name)', not 40087JoeFinApps.TextGrab."
    }
    return $manifest.OuterXml
}

function New-PackagedFixture {
    param([Parameter(Mandatory)][string]$Name)

    $directory = Join-Path $ArtifactDirectory 'fixtures'
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    return Join-Path $directory $Name
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Package was not found: $PackagePath"
}

$PackagePath = [IO.Path]::GetFullPath($PackagePath)
$extension = [IO.Path]::GetExtension($PackagePath).ToLowerInvariant()
if ($extension -notin @('.msix', '.appx', '.msixbundle', '.appxbundle')) {
    throw "Unsupported package artifact extension '$extension'. Supply .msix, .appx, .msixbundle, or .appxbundle."
}
$artifact = Get-PackageArtifactMetadata -Path $PackagePath
$manifest = $artifact.manifest

Invoke-PackagedScenario -Scenario 'package-artifact-contract' -SuccessReason 'Package artifact declares the required activation contracts.' -Script {
    foreach ($declaration in @(
        'windows.protocol',
        'windows.fileTypeAssociation',
        'windows.shareTarget',
        'windows.startupTask',
        'windows.toastNotificationActivation')) {
        Assert-ManifestContains -Manifest $manifest -Expected $declaration
    }
    [pscustomobject]@{
        packagePath = $PackagePath
        artifactType = $artifact.artifactType
        selectedPayload = $artifact.selectedPayload
        supportedArchitectures = $artifact.supportedArchitectures
    }
} | Out-Null

$lifecycleScenarios = @(
    'install-registration',
    'executable-launch-window-readiness',
    'protocol-activation',
    'file-association-txt',
    'file-association-png',
    'file-association-tggf',
    'startup-task-registration',
    'startup-task-runtime-state',
    'upgrade-local-state-preservation',
    'share-target-activation',
    'toast-click-activation',
    'uninstall-reinstall')

if ($script:HasFailures) {
    foreach ($scenario in $lifecycleScenarios) {
        Add-PackagedVmResult -Scenario $scenario -Outcome skip -Reason 'Package artifact contract failed; no installation or activation action was attempted.'
    }
    Write-PackagedVmResults
    exit 1
}

$gatingReason = if (-not $RunDestructiveLifecycle) {
    'No package lifecycle action was taken. Pass -RunDestructiveLifecycle only on a disposable VM.'
}
elseif (-not $ConfirmDisposableVm -or $env:TEXT_GRAB_DISPOSABLE_VM -ne '1') {
    'Refusing install/register/upgrade/uninstall. Set TEXT_GRAB_DISPOSABLE_VM=1 and pass -ConfirmDisposableVm on a resettable VM.'
}
else {
    $null
}

if ($gatingReason) {
    foreach ($scenario in $lifecycleScenarios) {
        Add-PackagedVmResult -Scenario $scenario -Outcome skip -Reason $gatingReason
    }
    Write-PackagedVmResults
    if ($script:HasFailures) {
        exit 1
    }
    return
}

$installedByThisRun = $false
$previousInstalledByThisRun = $false
try {
    Assert-PackageArtifactSignature -Path $PackagePath

    $existing = @(Get-AppxPackage -Name '40087JoeFinApps.TextGrab' -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) {
        throw "Refusing to alter an existing Text Grab package ($($existing[0].PackageFullName)). Use a resettable VM with no pre-existing Text Grab package."
    }

    if ($PreviousPackagePath) {
        $PreviousPackagePath = [IO.Path]::GetFullPath($PreviousPackagePath)
        if (-not (Test-Path -LiteralPath $PreviousPackagePath -PathType Leaf)) {
            throw "Previous package was not found: $PreviousPackagePath"
        }

        if (-not (Invoke-PackagedScenario -Scenario 'upgrade-old-package-registration' -SuccessReason 'Previous package installed and registered before upgrade.' -Script {
            Add-AppxPackage -Path $PreviousPackagePath -ForceApplicationShutdown
            $script:previousInstalledByThisRun = $true
            $oldPackage = Get-RegisteredTextGrabPackage
            [pscustomobject]@{ packageFullName = $oldPackage.PackageFullName; version = $oldPackage.Version.ToString() }
        })) {
            throw 'The previous package could not be installed; upgrade assertions cannot continue.'
        }

        $oldPackage = Get-RegisteredTextGrabPackage
        $localState = Join-Path $env:LOCALAPPDATA "Packages\$($oldPackage.PackageFamilyName)\LocalState\TextGrab.UiTests"
        New-Item -ItemType Directory -Path $localState -Force | Out-Null
        $upgradeSeedPath = Join-Path $localState 'upgrade-seed.json'
        $upgradeSeed = [guid]::NewGuid().ToString('N')
        @{ marker = $upgradeSeed; createdUtc = [DateTimeOffset]::UtcNow } | ConvertTo-Json | Set-Content -LiteralPath $upgradeSeedPath -Encoding utf8

        if (-not (Invoke-PackagedScenario -Scenario 'upgrade-local-state-preservation' -SuccessReason 'Upgrade retained the deterministic LocalState seed written by the old package.' -Script {
            Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown
            $script:installedByThisRun = $true
            $upgradedPackage = Get-RegisteredTextGrabPackage
            $installedManifest = Get-InstalledTextGrabManifest -Package $upgradedPackage
            Assert-ManifestContains -Manifest $installedManifest -Expected 'windows.protocol'
            Assert-ManifestContains -Manifest $installedManifest -Expected 'windows.fileTypeAssociation'
            Assert-ManifestContains -Manifest $installedManifest -Expected 'windows.startupTask'
            if ($upgradedPackage.Version -eq $oldPackage.Version) {
                throw "Upgrade did not change the package version ($($oldPackage.Version))."
            }
            $persisted = Get-Content -LiteralPath $upgradeSeedPath -Raw | ConvertFrom-Json
            if ($persisted.marker -ne $upgradeSeed) {
                throw 'The LocalState upgrade seed was not preserved.'
            }
            [pscustomobject]@{ oldVersion = $oldPackage.Version.ToString(); newVersion = $upgradedPackage.Version.ToString(); seedPath = $upgradeSeedPath }
        })) {
            throw 'Package upgrade failed; remaining activation assertions are not meaningful.'
        }
    }
    else {
        Add-PackagedVmResult -Scenario 'upgrade-local-state-preservation' -Outcome skip -Reason 'No -PreviousPackagePath was supplied, so there is no old/test package from which to assert upgrade data preservation.'
        if (-not (Invoke-PackagedScenario -Scenario 'install-registration' -SuccessReason 'Package installation created a registered Text Grab package.' -Script {
            Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown
            $script:installedByThisRun = $true
            $registered = Get-RegisteredTextGrabPackage
            $installedManifest = Get-InstalledTextGrabManifest -Package $registered
            foreach ($declaration in @(
                'windows.protocol',
                'windows.fileTypeAssociation',
                'windows.shareTarget',
                'windows.startupTask',
                'windows.toastNotificationActivation')) {
                Assert-ManifestContains -Manifest $installedManifest -Expected $declaration
            }
            [pscustomobject]@{
                packageFullName = $registered.PackageFullName
                packageFamilyName = $registered.PackageFamilyName
                version = $registered.Version.ToString()
                installLocation = $registered.InstallLocation
                artifactType = $artifact.artifactType
            }
        })) {
            throw 'Package installation failed; remaining activation assertions are not meaningful.'
        }
    }

    if ($PreviousPackagePath) {
        $registered = Get-RegisteredTextGrabPackage
        $installedManifest = Get-InstalledTextGrabManifest -Package $registered
        Add-PackagedVmResult -Scenario 'install-registration' -Outcome pass -Reason 'Upgraded package remains registered after installation.' -Details @{
            packageFullName = $registered.PackageFullName
            version = $registered.Version.ToString()
        }
    }

    $registered = Get-RegisteredTextGrabPackage
    if (-not (Invoke-PackagedScenario -Scenario 'executable-launch-window-readiness' -SuccessReason 'AppsFolder launch produced a ready Text Grab window.' -Script {
        Stop-TextGrabUnderTest
        $started = [DateTime]::UtcNow
        Start-Process "shell:AppsFolder\$($registered.PackageFamilyName)!App" -ErrorAction Stop
        Wait-TextGrabWindow -StartedAfter $started
    })) {
        throw 'AppsFolder launch failed; downstream shell activation assertions are not meaningful.'
    }

    Invoke-PackagedScenario -Scenario 'protocol-activation' -SuccessReason 'text-grab://edit-text activated the packaged Edit Text window.' -Script {
        Stop-TextGrabUnderTest
        $started = [DateTime]::UtcNow
        Start-Process 'text-grab://edit-text' -ErrorAction Stop
        Wait-TextGrabWindow -ExpectedTitle '*Edit Text*' -StartedAfter $started
    } | Out-Null

    $textFixture = New-PackagedFixture -Name 'packaged-association.txt'
    Set-Content -LiteralPath $textFixture -Value 'Text Grab packaged file-association fixture.' -Encoding utf8
    Invoke-PackagedScenario -Scenario 'file-association-txt' -SuccessReason 'OpenInTextGrab shell verb activated Edit Text for a .txt fixture.' -Script {
        Invoke-AssociationVerb -Path $textFixture -Verb 'OpenInTextGrab' -ExpectedTitle '*Edit Text*'
    } | Out-Null

    $imageSource = Join-Path $FixtureDirectory 'font_sample.png'
    if (Test-Path -LiteralPath $imageSource -PathType Leaf) {
        $imageFixture = New-PackagedFixture -Name 'packaged-association.png'
        Copy-Item -LiteralPath $imageSource -Destination $imageFixture -Force
        Invoke-PackagedScenario -Scenario 'file-association-png' -SuccessReason 'OpenInGrabFrame shell verb activated Grab Frame for a .png fixture.' -Script {
            Invoke-AssociationVerb -Path $imageFixture -Verb 'OpenInGrabFrame' -ExpectedTitle '*Grab Frame*'
        } | Out-Null
    }
    else {
        Add-PackagedVmResult -Scenario 'file-association-png' -Outcome skip -Reason "PNG fixture was not found: $imageSource"
    }

    $tggfCandidate = @(Get-ChildItem -LiteralPath $FixtureDirectory -Filter '*.tggf' -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1)[0]
    $tggfSource = if ($TggfFixturePath) { $TggfFixturePath } elseif ($tggfCandidate) { $tggfCandidate.FullName } else { $null }
    if ($tggfSource -and (Test-Path -LiteralPath $tggfSource -PathType Leaf)) {
        $tggfFixture = New-PackagedFixture -Name 'packaged-association.tggf'
        Copy-Item -LiteralPath $tggfSource -Destination $tggfFixture -Force
        Invoke-PackagedScenario -Scenario 'file-association-tggf' -SuccessReason 'OpenGrabFrame shell verb activated Grab Frame for a .tggf fixture.' -Script {
            Invoke-AssociationVerb -Path $tggfFixture -Verb 'OpenGrabFrame' -ExpectedTitle '*Grab Frame*'
        } | Out-Null
    }
    else {
        Add-PackagedVmResult -Scenario 'file-association-tggf' -Outcome skip -Reason 'No .tggf fixture is available. Supply -TggfFixturePath to execute this association assertion.'
    }

    Invoke-PackagedScenario -Scenario 'startup-task-registration' -SuccessReason 'The installed package is listed as the registered Text Grab app and declares StartTextGrab disabled by default.' -Script {
        $startApp = @(Get-StartApps | Where-Object { $_.AppID -eq "$($registered.PackageFamilyName)!App" })[0]
        if ($null -eq $startApp) {
            throw "Get-StartApps did not list $($registered.PackageFamilyName)!App after installation."
        }
        Assert-ManifestContains -Manifest $installedManifest -Expected 'TaskId="StartTextGrab"'
        Assert-ManifestContains -Manifest $installedManifest -Expected 'Enabled="false"'
        [pscustomobject]@{ appId = $startApp.AppID; name = $startApp.Name; declaredState = 'Disabled' }
    } | Out-Null

    Add-PackagedVmResult -Scenario 'startup-task-runtime-state' -Outcome skip -Reason 'The external PowerShell runner has no package identity, so StartupTask.GetAsync cannot query the package-scoped runtime state. Registration/default-disabled state is asserted separately.'
    Add-PackagedVmResult -Scenario 'share-target-activation' -Outcome skip -Reason 'No deterministic Share broker sender is available in this harness; the installed manifest contract is asserted, but no share activation is claimed.'
    Add-PackagedVmResult -Scenario 'toast-click-activation' -Outcome skip -Reason 'No deterministic Windows notification click driver is available in this harness; toast registration is asserted, but no click activation is claimed.'
    Add-PackagedVmResult -Scenario 'uninstall-reinstall' -Outcome skip -Reason 'Cleanup uninstalls the package, but reinstall/persistence behavior is not claimed without a resettable profile orchestrator.'
}
catch {
    Add-PackagedVmResult -Scenario 'lane-setup' -Outcome fail -Reason $_.Exception.Message -Details @{ exception = $_.Exception.ToString() }
}
finally {
    try {
        Stop-TextGrabUnderTest
        if ($installedByThisRun -or $previousInstalledByThisRun) {
            $registered = @(Get-AppxPackage -Name '40087JoeFinApps.TextGrab' -ErrorAction SilentlyContinue | Select-Object -First 1)[0]
            if ($registered) {
                Remove-AppxPackage -Package $registered.PackageFullName -ErrorAction Stop
            }
            if (Get-AppxPackage -Name '40087JoeFinApps.TextGrab' -ErrorAction SilentlyContinue) {
                throw 'Text Grab is still registered after disposable-VM cleanup uninstall.'
            }
        }
    }
    catch {
        Add-PackagedVmResult -Scenario 'cleanup-uninstall' -Outcome fail -Reason $_.Exception.Message -Details @{ exception = $_.Exception.ToString() }
    }
    Write-PackagedVmResults
}

if ($script:HasFailures) {
    exit 1
}
