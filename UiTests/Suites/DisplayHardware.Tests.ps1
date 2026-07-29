Import-Module "$PSScriptRoot\Capability.Helpers.psm1" -Force -Global -DisableNameChecking

Register-UiTest -Suite 'DisplayHardware' -Name 'coordinate-dpi-grid-reports-window-bounds-monitor-and-dpi' -RequiredCapabilities @('interactiveDesktop') -Script {
    param($Context)

    $fixture = Start-CapabilityFixture -Context $Context -Surface 'CoordinateDpi'
    Wait-UiTestElement -Target $fixture -AutomationId 'WindowState' -TimeoutSeconds $Context.TimeoutSeconds
    $statePath = Join-Path $Context.LogsDirectory 'fixture-CoordinateDpi-state.jsonl'
    Start-Sleep -Milliseconds 250
    $state = Get-CapabilityFixtureState -Path $statePath
    if ([uint32]$state.dpi -lt 96 -or [string]::IsNullOrWhiteSpace($state.bounds) -or $state.monitor -eq 'unavailable') {
        throw "Fixture did not report usable coordinate/DPI metrics: $($state | ConvertTo-Json -Compress)"
    }
    Save-UiTestScreenshot -Context $Context -Target $fixture -Name 'display-coordinate-dpi-grid' | Out-Null
}

Register-UiTest -Suite 'DisplayHardware' -Name 'fixture-window-movement-updates-monitor-and-bounds' -RequiredCapabilities @('interactiveDesktop') -Script {
    param($Context)

    $fixture = Start-CapabilityFixture -Context $Context -Surface 'CoordinateDpi'
    $statePath = Join-Path $Context.LogsDirectory 'fixture-CoordinateDpi-state.jsonl'
    $monitor = @($Context.Capabilities.display.monitors | Select-Object -First 1)[0]
    Move-CapabilityWindow -WindowHandle $fixture.WindowHandle -X ([int]$monitor.workingArea.x + 20) -Y ([int]$monitor.workingArea.y + 20)
    Start-Sleep -Milliseconds 500
    $state = Get-CapabilityFixtureState -Path $statePath
    Assert-CapabilityContains -Actual $state.monitor -Expected $monitor.deviceName -Description 'moved fixture monitor'
    if ($state.bounds -eq '100,100,1000x780') {
        throw "Fixture bounds did not change after SetWindowPos: $($state.bounds)"
    }
    Save-UiTestScreenshot -Context $Context -Target $fixture -Name 'display-moved-fixture' | Out-Null
}

Register-UiTest -Suite 'DisplayHardware' -Name 'mixed-dpi-second-monitor-reflow-and-screenshot' -RequiredCapabilities @('multiMonitor', 'mixedDpi', 'interactiveDesktop') -Script {
    param($Context)

    $fixture = Start-CapabilityFixture -Context $Context -Surface 'CoordinateDpi'
    $statePath = Join-Path $Context.LogsDirectory 'fixture-CoordinateDpi-state.jsonl'
    $monitor = @($Context.Capabilities.display.monitors | Select-Object -Skip 1 -First 1)[0]
    Move-CapabilityWindow -WindowHandle $fixture.WindowHandle -X ([int]$monitor.workingArea.x + 20) -Y ([int]$monitor.workingArea.y + 20)
    Start-Sleep -Milliseconds 500
    $state = Get-CapabilityFixtureState -Path $statePath
    Assert-CapabilityContains -Actual $state.monitor -Expected $monitor.deviceName -Description 'second-monitor fixture placement'
    if ([uint32]$state.dpi -ne [uint32]$monitor.dpi) {
        throw "Fixture reported DPI $($state.dpi), expected second-monitor DPI $($monitor.dpi)."
    }
    Save-UiTestScreenshot -Context $Context -Target $fixture -Name 'display-mixed-dpi-second-monitor' | Out-Null
}

Register-UiTest -Suite 'DisplayHardware' -Name 'contrast-surface-reflects-theme-and-high-contrast-capability' -RequiredCapabilities @('interactiveDesktop') -Script {
    param($Context)

    $fixture = Start-CapabilityFixture -Context $Context -Surface 'Contrast'
    Wait-UiTestElement -Target $fixture -AutomationId 'WindowState' -TimeoutSeconds $Context.TimeoutSeconds
    Save-UiTestScreenshot -Context $Context -Target $fixture -Name "display-$($Context.Capabilities.display.theme)-highcontrast-$($Context.Capabilities.display.highContrast)" | Out-Null
}

Register-UiTest -Suite 'DisplayHardware' -Name 'hdr-capture-fixture-is-available-on-hdr-monitors' -RequiredCapabilities @('hdr', 'interactiveDesktop') -Script {
    param($Context)

    $fixture = Start-CapabilityFixture -Context $Context -Surface 'Contrast'
    Wait-UiTestElement -Target $fixture -AutomationId 'WindowState' -TimeoutSeconds $Context.TimeoutSeconds
    Save-UiTestScreenshot -Context $Context -Target $fixture -Name 'display-hdr-contrast-fixture' | Out-Null
}
