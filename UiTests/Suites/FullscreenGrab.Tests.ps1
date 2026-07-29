Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

# Fullscreen Grab is Text Grab's flagship capture experience, and it is fundamentally a
# real-input flow: the user drags a rectangle over the screen and the selection is OCR'd.
# The real region drag and toolbar interaction below drive physical mouse input on an
# isolated interactive desktop (the same capability contract as the SystemIntegration
# suite) and skip safely when real-input automation is unavailable. The auto-hiding top
# toolbar only appears after pointer motion, so any toolbar interaction is preceded by a
# small mouse move to reveal it. A single deterministic smoke keeps always-on coverage
# that the overlay launches at all, asserting only the structure present before any move.

Register-UiTest -Suite 'FullscreenGrab' -Name 'overlay-launches-with-selection-canvas' -Script {
    param($Context)

    # Deterministic smoke: the overlay window and its selection surface are present at
    # launch without any pointer input. The auto-hiding toolbar is intentionally not
    # asserted here; it is covered by the real-input reveal test below.
    $fullscreen = Start-DeterministicTextGrab -Context $Context -Arguments @('Fullscreen') -WindowTitle 'Text Grab'
    Wait-UiTestElement -Target $fullscreen -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $fullscreen -AutomationId 'FullscreenGrab.SelectionCanvas' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestWindow -ProcessId $fullscreen.ProcessId -WindowHandle $fullscreen.WindowHandle
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'FullscreenGrab' -Name 'real-region-drag-selects-known-fixture-text-to-clipboard' -Script {
    param($Context)

    Initialize-SystemIntegration $Context
    $editor = Ensure-SystemEditor $Context
    Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.FullscreenGrab'
    $overlay = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds

    $fixture = Get-SystemElementCenter -Target $Context.Fixture -AutomationId 'KnownTextDisplay'
    $snapshot = Get-UiTestClipboardSnapshot
    try {
        # Physically click-drag a rectangle around the known fixture text. The flagship
        # region grab recognizes the selection and copies the text to the clipboard.
        Invoke-SystemIntegrationHelper -Arguments @('--drag',
            [string]($fixture.X - 140), [string]($fixture.Y - 26),
            [string]($fixture.X + 280), [string]($fixture.Y + 46)) | Out-Null
        Start-Sleep -Milliseconds 700
        if (-not [System.Windows.Clipboard]::ContainsText()) {
            Save-UiTestScreenshot -Context $Context -Target $overlay -Name 'fullscreen-region-no-clipboard' -CaptureScreen | Out-Null
            throw 'Fullscreen region drag did not copy any recognized text to the clipboard.'
        }
    }
    finally {
        Restore-UiTestClipboardSnapshot -Snapshot $snapshot
    }
}

Register-UiTest -Suite 'FullscreenGrab' -Name 'toolbar-reveals-on-mouse-move-and-capture-mode-is-settable' -Script {
    param($Context)

    Initialize-SystemIntegration $Context
    $editor = Ensure-SystemEditor $Context
    Invoke-UiTestElement -Target $editor -AutomationId 'EditText.Menu.FullscreenGrab'
    $overlay = Get-SystemWindow -ProcessId $Context.TextGrab.ProcessId -AutomationId 'FullscreenGrabWindow' -TimeoutSeconds $Context.TimeoutSeconds

    # The top toolbar auto-hides until the pointer moves; nudge the cursor over the overlay
    # so its controls become visible and hit-testable before interacting with them.
    $canvas = Get-SystemElementCenter -Target $overlay -AutomationId 'FullscreenGrab.SelectionCanvas'
    Invoke-SystemIntegrationHelper -Arguments @('--move', [string]$canvas.X, [string]$canvas.Y) | Out-Null
    foreach ($automationId in @(
        'FullscreenGrab.StandardModeToggle',
        'FullscreenGrab.SingleLineToggle',
        'FullscreenGrab.TableToggle',
        'FullscreenGrab.SelectionStyle',
        'FullscreenGrab.AcceptSelectionButton',
        'FullscreenGrab.CancelButton'
    )) {
        Wait-UiTestElement -Target $overlay -AutomationId $automationId -TimeoutSeconds $Context.TimeoutSeconds
    }
    Invoke-UiTestElement -Target $overlay -AutomationId 'FullscreenGrab.SingleLineToggle'
    Assert-UiTestWindow -ProcessId $overlay.ProcessId -WindowHandle $overlay.WindowHandle

    Invoke-SystemIntegrationHelper -Arguments @('--escape') | Out-Null
    Wait-UiTestElement -Target $overlay -AutomationId 'FullscreenGrabWindow' -Gone -TimeoutSeconds $Context.TimeoutSeconds
}
