Import-Module "$PSScriptRoot\Capability.Helpers.psm1" -Force -Global -DisableNameChecking

Register-UiTest -Suite 'PackagedVm' -Name 'package-manifest-declares-protocol-file-share-startup-and-toast-contracts' -RequiredCapabilities @('packageSupport') -Script {
    param($Context)

    $manifestPath = Join-Path $PSScriptRoot '..\..\Text-Grab-Package\Package.appxmanifest'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw
    foreach ($declaration in @('windows.protocol', 'windows.fileTypeAssociation', 'windows.shareTarget', 'windows.startupTask', 'windows.toastNotificationActivation')) {
        Assert-CapabilityContains -Actual $manifest -Expected $declaration -Description 'package activation declaration'
    }
}

Register-UiTest -Suite 'PackagedVm' -Name 'registered-package-exposes-version-and-activation-surface' -RequiredCapabilities @('packaged', 'packageSupport') -Script {
    param($Context)

    $package = $Context.Capabilities.capabilities.packaged.value
    if ([string]::IsNullOrWhiteSpace([string]$package.PackageFullName) -or [string]::IsNullOrWhiteSpace([string]$package.InstallLocation)) {
        throw 'The registered Text Grab package did not expose a package full name and install location.'
    }
}

Register-UiTest -Suite 'PackagedVm' -Name 'disposable-vm-script-has-scenario-results-and-strong-gating' -Script {
    param($Context)

    $script = Join-Path $PSScriptRoot '..\Scripts\Invoke-PackagedVmLane.ps1'
    Assert-UiTestFile -Path $script
    $content = Get-Content -LiteralPath $script -Raw
    foreach ($scenario in @(
        'install-registration',
        'executable-launch-window-readiness',
        'protocol-activation',
        'file-association-txt',
        'startup-task-registration',
        'upgrade-local-state-preservation',
        'share-target-activation',
        'toast-click-activation',
        'uninstall-reinstall')) {
        Assert-CapabilityContains -Actual $content -Expected $scenario -Description 'packaged VM scenario result'
    }
    Assert-CapabilityContains -Actual $content -Expected 'TEXT_GRAB_DISPOSABLE_VM' -Description 'disposable VM gate'
}

Register-UiTest -Suite 'PackagedVm' -Name 'registered-package-exercises-activation-only-on-disposable-vm' -RequiredCapabilities @('packaged', 'disposableVm') -Script {
    param($Context)

    $script = Join-Path $PSScriptRoot '..\Scripts\Invoke-PackagedVmLane.ps1'
    Assert-UiTestFile -Path $script
}
