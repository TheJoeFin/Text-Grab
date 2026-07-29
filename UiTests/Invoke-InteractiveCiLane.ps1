[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('smoke-unpackaged-x64', 'system-integration', 'display-mixed-dpi', 'packaged', 'arm64', 'copilot-plus-winai')]
    [string]$Lane,
    [Parameter(Mandatory)][string]$ArtifactRoot,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$Record,
    [switch]$SkipXunit,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
$diagnosticsDirectory = Join-Path $artifactRoot 'diagnostics'
$xunitDirectory = Join-Path $artifactRoot 'xunit'
New-Item -ItemType Directory -Path $diagnosticsDirectory, $xunitDirectory -Force | Out-Null

$laneSettings = @{
    'smoke-unpackaged-x64' = @{ Suites = @('Harness', 'Lifecycle', 'CliProtocol'); Platform = 'x64'; SystemIntegration = $false; Package = $false; PackageLifecycle = $false; RequiredCapabilities = @(); RequiresDisposableVm = $false }
    'system-integration' = @{ Suites = @('SystemIntegration'); Platform = 'x64'; SystemIntegration = $true; Package = $false; PackageLifecycle = $false; RequiredCapabilities = @('interactiveDesktop'); RequiresDisposableVm = $false }
    'display-mixed-dpi' = @{ Suites = @('Display'); Platform = 'x64'; SystemIntegration = $false; Package = $false; PackageLifecycle = $false; RequiredCapabilities = @('interactiveDesktop', 'multiMonitor', 'mixedDpi'); RequiresDisposableVm = $false }
    'packaged' = @{ Suites = @('PackagedVm'); Platform = 'x64'; SystemIntegration = $false; Package = $true; PackageLifecycle = $true; RequiredCapabilities = @('packageSupport', 'disposableVm'); RequiresDisposableVm = $true }
    'arm64' = @{ Suites = @('Harness', 'Architecture', 'Ocr'); Platform = 'ARM64'; SystemIntegration = $false; Package = $false; PackageLifecycle = $false; RequiredCapabilities = @('arm64', 'winrtOcr'); RequiresDisposableVm = $false }
    'copilot-plus-winai' = @{ Suites = @('Architecture', 'Ocr', 'GrabFrame', 'NotificationTtsShare'); Platform = 'ARM64'; SystemIntegration = $false; Package = $true; PackageLifecycle = $false; RequiredCapabilities = @('arm64', 'winrtOcr', 'windowsAi'); RequiresDisposableVm = $false }
}
$settings = $laneSettings[$Lane]
$diagnosticsPath = Join-Path $diagnosticsDirectory 'lane.json'
$transcriptPath = Join-Path $diagnosticsDirectory 'lane-transcript.txt'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Find-PackagedArtifact {
    param([Parameter(Mandatory)][string]$Directory)

    $package = @(Get-ChildItem -LiteralPath $Directory -File -Recurse -Filter '*.msixbundle' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1)
    if ($package.Count -eq 0) {
        $package = @(Get-ChildItem -LiteralPath $Directory -File -Recurse -Filter '*.msix' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1)
    }
    if ($package.Count -ne 1) {
        throw "No MSIX or MSIX bundle was produced under '$Directory'."
    }
    return $package[0].FullName
}

function Test-PackagedSigningPlan {
    $winapp = Get-Command winapp -ErrorAction SilentlyContinue
    if ($null -eq $winapp) {
        throw 'The packaged lane requires WinApp CLI for ephemeral test certificate generation and MSIX signing.'
    }

    $manifestPath = Join-Path $repositoryRoot 'Text-Grab-Package\Package.appxmanifest'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Package manifest was not found: $manifestPath"
    }
    foreach ($arguments in @(
        @('cert', 'generate', '--help'),
        @('sign', '--help')
    )) {
        & winapp @arguments 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "WinApp does not support the required packaged-lane command: winapp $($arguments -join ' ')."
        }
    }

    [ordered]@{
        tool = $winapp.Source
        manifest = $manifestPath
        certificate = 'winapp cert generate --manifest <manifest> --output <per-run-pfx> --password <generated-password>'
        trustStore = 'CurrentUser\TrustedPeople'
        certificateValidityDays = 2
        signing = 'winapp sign <msix-or-msixbundle> <per-run-pfx> --password <generated-password>'
        verification = 'Get-AuthenticodeSignature status=Valid and signer thumbprint matches the generated certificate'
        cleanup = 'Remove the generated certificate from CurrentUser\TrustedPeople and delete the PFX/CER after lifecycle cleanup.'
    }
}

function Invoke-WinAppPrivate {
    param([Parameter(Mandatory)][string[]]$Arguments, [switch]$Sensitive)

    $output = (& winapp @Arguments 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        if ($Sensitive) {
            throw "WinApp command failed with exit code ${LASTEXITCODE}."
        }
        throw "WinApp command failed with exit code ${LASTEXITCODE}: $output"
    }
}

function New-EphemeralMsixSigningCertificate {
    param(
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][string]$Directory
    )

    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $pfxPath = Join-Path $Directory 'text-grab-ui-test.pfx'
    $cerPath = Join-Path $Directory 'text-grab-ui-test.cer'
    $passwordBytes = [byte[]]::new(32)
    [Security.Cryptography.RandomNumberGenerator]::Fill($passwordBytes)
    $password = [Convert]::ToBase64String($passwordBytes)

    Invoke-WinAppPrivate -Sensitive -Arguments @(
        'cert', 'generate',
        '--manifest', $ManifestPath,
        '--output', $pfxPath,
        '--password', $password,
        '--valid-days', '2',
        '--if-exists', 'overwrite',
        '--quiet'
    )
    if (-not (Test-Path -LiteralPath $pfxPath -PathType Leaf)) {
        throw "WinApp did not create the requested test signing PFX: $pfxPath"
    }

    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $pfxPath,
        $password,
        [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
    )
    if (-not $certificate.HasPrivateKey) {
        throw 'The generated MSIX signing certificate has no private key.'
    }
    [IO.File]::WriteAllBytes($cerPath, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))

    [pscustomobject]@{
        PfxPath = $pfxPath
        CerPath = $cerPath
        Password = $password
        Thumbprint = $certificate.Thumbprint
    }
}

function Add-EphemeralCertificateTrust {
    param([Parameter(Mandatory)]$SigningCertificate)

    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($SigningCertificate.CerPath)
    $store = [Security.Cryptography.X509Certificates.X509Store]::new(
        [Security.Cryptography.X509Certificates.StoreName]::TrustedPeople,
        [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    )
    try {
        $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $store.Add($certificate)
    }
    finally {
        $store.Close()
        $certificate.Dispose()
    }
}

function Remove-EphemeralCertificateTrust {
    param([Parameter(Mandatory)][string]$Thumbprint)

    $store = [Security.Cryptography.X509Certificates.X509Store]::new(
        [Security.Cryptography.X509Certificates.StoreName]::TrustedPeople,
        [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    )
    try {
        $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        foreach ($certificate in @($store.Certificates.Find(
            [Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $Thumbprint,
            $false
        ))) {
            $store.Remove($certificate)
        }
    }
    finally {
        $store.Close()
    }
}

function Sign-AndVerifyMsixArtifact {
    param(
        [Parameter(Mandatory)][string]$PackagePath,
        [Parameter(Mandatory)]$SigningCertificate
    )

    Invoke-WinAppPrivate -Sensitive -Arguments @(
        'sign', $PackagePath, $SigningCertificate.PfxPath,
        '--password', $SigningCertificate.Password,
        '--quiet'
    )
    $signature = Get-AuthenticodeSignature -FilePath $PackagePath
    if ($signature.Status -ne 'Valid') {
        throw "MSIX signature verification failed for '$PackagePath': $($signature.Status) $($signature.StatusMessage)"
    }
    if ($signature.SignerCertificate.Thumbprint -ne $SigningCertificate.Thumbprint) {
        throw "MSIX signer '$($signature.SignerCertificate.Thumbprint)' does not match generated certificate '$($SigningCertificate.Thumbprint)'."
    }
}

function Write-RunnerFailureUiReports {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)]$State
    )

    if (@(Get-ChildItem -LiteralPath $Root -Filter 'results.json' -File -Recurse -ErrorAction SilentlyContinue).Count -gt 0) {
        return
    }

    $outputDirectory = Join-Path $Root 'ui\runner-failure\output'
    New-Item -ItemType Directory -Path $outputDirectory, (Join-Path $Root 'ui\runner-failure\screenshots'), (Join-Path $Root 'ui\runner-failure\recordings') -Force | Out-Null
    $details = if ($State.error) { [string]$State.error } else { 'The UI runner did not produce a report.' }
    [ordered]@{
        runId = 'runner-failure'
        startedUtc = $State.startedUtc
        completedUtc = $State.completedUtc
        failed = 1
        results = @([ordered]@{
            suite = 'InteractiveCi'
            name = 'Runner setup'
            outcome = 'fail'
            durationSeconds = 0
            details = $details
            screenshot = $null
        })
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputDirectory 'results.json') -Encoding utf8
    [ordered]@{
        lane = $State.lane
        configuration = $State.configuration
        platform = $State.platform
        error = $details
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputDirectory 'environment.json') -Encoding utf8

    $escaped = [Security.SecurityElement]::Escape($details)
    @"
<?xml version="1.0" encoding="utf-8"?>
<testsuite name="InteractiveCi" tests="1" failures="1" skipped="0" time="0">
  <testcase classname="InteractiveCi" name="Runner setup" time="0">
    <failure message="UI runner setup failed">$escaped</failure>
  </testcase>
</testsuite>
"@ | Set-Content -LiteralPath (Join-Path $outputDirectory 'junit.xml') -Encoding utf8
}

function Write-DryRunUiReports {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)]$State
    )

    $outputDirectory = Join-Path $Root 'ui\dry-run\output'
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $details = 'Configuration validation only; no build, UI automation, or package lifecycle action was performed.'
    [ordered]@{
        runId = 'dry-run'
        startedUtc = $State.startedUtc
        completedUtc = $State.completedUtc
        failed = 0
        results = @([ordered]@{
            suite = 'InteractiveCi'
            name = 'Configuration validation'
            outcome = 'skip'
            durationSeconds = 0
            details = $details
            screenshot = $null
        })
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputDirectory 'results.json') -Encoding utf8
    $State | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputDirectory 'environment.json') -Encoding utf8
    @"
<?xml version="1.0" encoding="utf-8"?>
<testsuite name="InteractiveCi" tests="1" failures="0" skipped="1" time="0">
  <testcase classname="InteractiveCi" name="Configuration validation" time="0">
    <skipped message="$details" />
  </testcase>
</testsuite>
"@ | Set-Content -LiteralPath (Join-Path $outputDirectory 'junit.xml') -Encoding utf8
}

$suiteAliases = @{
    Display = 'DisplayHardware'
    Ocr = 'OcrCapability'
    Architecture = 'OsArchitecture'
    NotificationTtsShare = 'NotificationsTtsShare'
}
$resolvedSuites = @($settings.Suites | ForEach-Object {
    if ($suiteAliases.ContainsKey($_)) { $suiteAliases[$_] } else { $_ }
})
$runState = [ordered]@{
    lane = $Lane
    startedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    configuration = $Configuration
    platform = $settings.Platform
    suites = $resolvedSuites
    systemIntegration = $settings.SystemIntegration
    packageBuild = $settings.Package
    packageLifecycle = $settings.PackageLifecycle
    requiredCapabilities = $settings.RequiredCapabilities
    requiresDisposableVm = $settings.RequiresDisposableVm
    configuredRunnerCapabilities = $env:TEXT_GRAB_INTERACTIVE_CAPABILITIES
    xunit = -not $SkipXunit
    record = [bool]$Record
    dryRun = [bool]$DryRun
    completedUtc = $null
    error = $null
}
if ($settings.PackageLifecycle) {
    $runState.signingPlan = Test-PackagedSigningPlan
}

if ($DryRun) {
    $runState.completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $runState | ConvertTo-Json -Depth 8 | Tee-Object -FilePath $diagnosticsPath
    Write-DryRunUiReports -Root $artifactRoot -State $runState
    return
}

Start-Transcript -LiteralPath $transcriptPath -Force | Out-Null
$transcriptStarted = $true
$ephemeralSigningDirectory = $null
$signingCertificate = $null
try {
    Push-Location $repositoryRoot

    if ($settings.RequiresDisposableVm -and $env:TEXT_GRAB_DISPOSABLE_VM -ne '1') {
        throw "Lane '$Lane' requires TEXT_GRAB_DISPOSABLE_VM=1 from its dedicated resettable runner."
    }

    Invoke-DotNet -Arguments @('restore', 'Text-Grab.sln', '--nologo')
    Invoke-DotNet -Arguments @('build', 'Text-Grab\Text-Grab.csproj', '-c', $Configuration, '-p:EnableMsixTooling=true', "-p:Platform=$($settings.Platform)", '--nologo')
    Invoke-DotNet -Arguments @('build', 'UiTests\TextGrab.AutomationHost\TextGrab.AutomationHost.csproj', '-c', $Configuration, "-p:Platform=$($settings.Platform)", '--nologo')
    Invoke-DotNet -Arguments @('build', 'UiTests\TextGrab.SystemIntegrationHelper\TextGrab.SystemIntegrationHelper.csproj', '-c', $Configuration, "-p:Platform=$($settings.Platform)", '--nologo')

    if ($settings.Package) {
        $packageDirectory = Join-Path $artifactRoot 'package'
        New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
        Invoke-DotNet -Arguments @('build', 'Text-Grab-Package\Text-Grab-Package.wapproj', '-c', $Configuration, "-p:Platform=$($settings.Platform)", '-p:AppxPackageSigningEnabled=false', '-p:GenerateAppxPackageOnBuild=true', "-p:AppxPackageDir=$packageDirectory\", '--nologo')
    }

    if (-not $SkipXunit) {
        Invoke-DotNet -Arguments @(
            'test', 'Tests\Tests.csproj', '-c', $Configuration, "-p:Platform=$($settings.Platform)",
            '--logger', 'trx;LogFileName=xunit.trx', '--results-directory', $xunitDirectory, '--nologo'
        )
    }

    $uiArguments = @(
        '-NoProfile', '-File', (Join-Path $PSScriptRoot 'Run-UiTests.ps1'),
        '-Configuration', $Configuration,
        '-ArtifactRoot', (Join-Path $artifactRoot 'ui')
    )
    if ($settings.RequiredCapabilities.Count -gt 0) {
        $uiArguments += @('-RequiredCapability') + $settings.RequiredCapabilities
    }
    $uiArguments += @('-Suite') + $settings.Suites + @('-NoBuild')
    if ($settings.SystemIntegration) { $uiArguments += '-SystemIntegration' }
    if ($Record) { $uiArguments += '-Record' }

    & pwsh @uiArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Run-UiTests.ps1 failed with exit code $LASTEXITCODE."
    }

    if ($settings.PackageLifecycle) {
        $packagePath = Find-PackagedArtifact -Directory $packageDirectory
        $ephemeralSigningDirectory = Join-Path $packageDirectory 'ephemeral-signing'
        Stop-Transcript | Out-Null
        $transcriptStarted = $false
        try {
            $signingCertificate = New-EphemeralMsixSigningCertificate -ManifestPath (Join-Path $repositoryRoot 'Text-Grab-Package\Package.appxmanifest') -Directory $ephemeralSigningDirectory
            Add-EphemeralCertificateTrust -SigningCertificate $signingCertificate
            Sign-AndVerifyMsixArtifact -PackagePath $packagePath -SigningCertificate $signingCertificate
            $signingCertificate.Password = $null
            $runState.signing = [ordered]@{
                packagePath = $packagePath
                packageExtension = [IO.Path]::GetExtension($packagePath)
                signerThumbprint = $signingCertificate.Thumbprint
                trustStore = 'CurrentUser\TrustedPeople'
                signatureStatus = 'Valid'
            }
        }
        finally {
            Start-Transcript -LiteralPath $transcriptPath -Append | Out-Null
            $transcriptStarted = $true
        }

        $lifecycleArguments = @(
            '-NoProfile', '-File', (Join-Path $PSScriptRoot 'Scripts\Invoke-PackagedVmLane.ps1'),
            '-PackagePath', $packagePath,
            '-RunDestructiveLifecycle',
            '-ConfirmDisposableVm',
            '-ArtifactDirectory', (Join-Path $artifactRoot 'package-lifecycle')
        )
        if (-not [string]::IsNullOrWhiteSpace($env:TEXT_GRAB_PREVIOUS_PACKAGE_PATH)) {
            $lifecycleArguments += @('-PreviousPackagePath', $env:TEXT_GRAB_PREVIOUS_PACKAGE_PATH)
        }
        & pwsh @lifecycleArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Invoke-PackagedVmLane.ps1 failed with exit code $LASTEXITCODE."
        }
    }
}
catch {
    $runState.error = $_.Exception.ToString()
    throw
}
finally {
    $cleanupFailure = $null
    try {
        if ($null -ne $signingCertificate) {
            Remove-EphemeralCertificateTrust -Thumbprint $signingCertificate.Thumbprint
        }
        if ($ephemeralSigningDirectory -and (Test-Path -LiteralPath $ephemeralSigningDirectory)) {
            Remove-Item -LiteralPath $ephemeralSigningDirectory -Recurse -Force
        }
    }
    catch {
        $cleanupFailure = $_.Exception
        $runState.error = if ($runState.error) { "$($runState.error)`r`nCertificate cleanup failed: $cleanupFailure" } else { "Certificate cleanup failed: $cleanupFailure" }
    }
    $runState.completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $runState | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $diagnosticsPath -Encoding utf8
    Write-RunnerFailureUiReports -Root $artifactRoot -State $runState
    Pop-Location -ErrorAction SilentlyContinue
    if ($transcriptStarted) {
        Stop-Transcript | Out-Null
    }
    if ($null -ne $cleanupFailure) {
        throw $cleanupFailure
    }
}
