Import-Module "$PSScriptRoot\Capability.Helpers.psm1" -Force -Global -DisableNameChecking

Register-UiTest -Suite 'OsArchitecture' -Name 'os-build-and-architecture-manifest-is-structured' -RequiredCapabilities @('windows') -Script {
    param($Context)

    $manifest = $Context.Capabilities
    if ([int]$manifest.os.build -lt 17763) {
        throw "Text Grab requires Windows build 17763 or newer; capability manifest reported $($manifest.os.build)."
    }
    if ([string]::IsNullOrWhiteSpace($manifest.os.architecture) -or [string]::IsNullOrWhiteSpace($manifest.packageManifestVersion)) {
        throw "OS/architecture or package-manifest version is missing from the capability manifest."
    }
}

Register-UiTest -Suite 'OsArchitecture' -Name 'windows-10-lane-keeps-windows-ai-hidden' -RequiredCapabilities @('windows10') -Script {
    param($Context)

    if ($Context.Capabilities.capabilities.windowsAi.available) {
        throw 'Windows AI must not be reported as available on Windows 10.'
    }
    Assert-CapabilityContains -Actual $Context.Capabilities.capabilities.windowsAi.reason -Expected 'Windows 10' -Description 'Windows AI Windows 10 reason'
}

Register-UiTest -Suite 'OsArchitecture' -Name 'windows-11-x64-lane-reports-windows-ai-architecture-precondition' -RequiredCapabilities @('windows11', 'x64') -Script {
    param($Context)

    if ($Context.Capabilities.capabilities.windowsAi.available) {
        throw 'Windows AI must not be exposed on x64 without the app debug architecture override.'
    }
    Assert-CapabilityContains -Actual $Context.Capabilities.capabilities.windowsAi.reason -Expected 'ARM64' -Description 'Windows AI x64 reason'
}

Register-UiTest -Suite 'OsArchitecture' -Name 'windows-11-arm64-lane-reports-packaging-or-windows-ai-readiness' -RequiredCapabilities @('windows11', 'arm64') -Script {
    param($Context)

    $windowsAi = $Context.Capabilities.capabilities.windowsAi
    if (-not $windowsAi.available -and [string]::IsNullOrWhiteSpace($windowsAi.reason)) {
        throw 'ARM64 Windows AI unavailability must include a precise preflight reason.'
    }
}
