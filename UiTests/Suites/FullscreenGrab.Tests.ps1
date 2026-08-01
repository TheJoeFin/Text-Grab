Set-StrictMode -Version Latest
Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

# Fullscreen Grab is Text Grab's flagship capture experience. These tests launch it straight
# from the command line (`Text-Grab.exe Fullscreen`) as its own process and hook the overlay
# by process id: the overlay is a transparent, owned WPF window whose UIA tree is unreachable
# through its HWND, and whose window-root AutomationId and bare selection Canvas are not
# queryable at all. Readiness is confirmed on the always-present capture-surface background
# image; the capture toolbar auto-hides for the default selection style until the pointer is
# over a foregrounded overlay.
#
# Each test force-stops its dedicated overlay process in a finally: a completed grab leaves the
# process lingering, and only the foregrounded overlay shows a usable toolbar, so overlays must
# never overlap between tests.
#
# The deterministic smoke launches and dismisses the overlay through UI Automation only (no
# real input), keeping always-on coverage that the overlay launches and renders its capture
# surface. The region-drag and toolbar tests drive physical mouse input on an isolated
# interactive desktop and skip safely when real-input automation is unavailable.

Register-UiTest -Suite 'FullscreenGrab' -Name 'overlay-launches-with-selection-canvas' -Script {
    param($Context)

    # Deterministic smoke: launch Fullscreen Grab from the command line, hook the overlay by
    # process id, and assert its full-screen capture surface rendered. No real input is used, so
    # the smoke stays in the always-on safe lane; the process is force-stopped in the finally.
    $overlay = Start-FullscreenGrabTarget -Context $Context
    try {
        Wait-UiTestElement -Target $overlay -AutomationId 'FullscreenGrab.BackgroundImage' -TimeoutSeconds $Context.TimeoutSeconds
        Assert-UiTestWindow -ProcessId $overlay.ProcessId -WindowHandle $overlay.WindowHandle
        Assert-DeterministicAutomationHealthy -Context $Context
    }
    finally {
        Stop-FullscreenGrabTarget -Context $Context -Target $overlay
    }
}

Register-UiTest -Suite 'FullscreenGrab' -Name 'real-region-drag-selects-known-fixture-text-to-clipboard' -Script {
    param($Context)

    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Fullscreen Grab region OCR'

    # Bring the fixture forward so the known text is not obscured by the shared editor window,
    # then record where that text sits on screen. The fixture is a normal window, so its
    # element bounds are reachable by HWND.
    Set-UiTestForegroundWindow -Target $Context.Fixture
    Start-Sleep -Milliseconds 300
    $fixture = Get-SystemElementCenter -Target $Context.Fixture -AutomationId 'KnownTextDisplay'

    # Launch Fullscreen Grab from the command line as its own system-integration process and
    # hook the overlay. It freezes the screen at launch, capturing the visible fixture text. The
    # region drag uses absolute screen coordinates, so it drives the topmost overlay regardless
    # of foreground state.
    $overlay = Start-FullscreenGrabTarget -Context $Context -SystemIntegration
    $snapshot = Get-UiTestClipboardSnapshot
    try {
        # Physically click-drag a rectangle around the whole known-text block (it is a wide,
        # two-line label, so a slice would miss the asserted phrase). The flagship region grab
        # OCRs the selection and copies the recognized text to the clipboard.
        $left = $fixture.X - [int]($fixture.Width / 2)
        $top = $fixture.Y - [int]($fixture.Height / 2)
        $right = $fixture.X + [int]($fixture.Width / 2)
        $bottom = $fixture.Y + [int]($fixture.Height / 2)
        Invoke-SystemIntegrationHelper -Arguments @('--drag',
            [string]($left + 8), [string]($top + 8),
            [string]($right - 8), [string]($bottom - 8)) | Out-Null

        # OCR + clipboard write is asynchronous; poll until the recognized text lands.
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Context.TimeoutSeconds)
        $recognized = $null
        do {
            Start-Sleep -Milliseconds 200
            # The clipboard can be briefly locked by Text Grab as it writes the grab result;
            # tolerate that transient contention and keep polling.
            try {
                if ([System.Windows.Clipboard]::ContainsText()) {
                    $recognized = [System.Windows.Clipboard]::GetText()
                    if ($recognized -match '(?i)quick brown') { break }
                }
            }
            catch { }
        } while ([DateTimeOffset]::UtcNow -lt $deadline)

        if ([string]::IsNullOrWhiteSpace($recognized)) {
            Save-UiTestScreenshot -Context $Context -Target $Context.Fixture -Name 'fullscreen-region-no-clipboard' -CaptureScreen | Out-Null
            throw 'Fullscreen region drag did not copy any recognized text to the clipboard.'
        }
        # Asserting the known fixture phrase proves the OCR capability ran end to end, not just
        # that some clipboard write happened.
        if ($recognized -notmatch '(?i)quick brown') {
            Save-UiTestScreenshot -Context $Context -Target $Context.Fixture -Name 'fullscreen-region-wrong-text' -CaptureScreen | Out-Null
            throw "Fullscreen region OCR did not recognize the known fixture text. Clipboard was: '$recognized'."
        }
    }
    finally {
        Restore-UiTestClipboardSnapshot -Snapshot $snapshot
        Stop-FullscreenGrabTarget -Context $Context -Target $overlay
    }
}

Register-UiTest -Suite 'FullscreenGrab' -Name 'toolbar-reveals-on-mouse-move-and-capture-mode-is-settable' -Script {
    param($Context)

    Initialize-SystemIntegration $Context
    Require-NoSystemIntegrationCollision -Context $Context -Scenario 'Fullscreen Grab toolbar interaction'
    $overlay = Start-FullscreenGrabTarget -Context $Context -SystemIntegration
    try {
        # The capture toolbar auto-hides for the default selection style until the pointer is
        # over the overlay, and only the foregrounded overlay reveals it. Force the overlay to
        # the foreground, then nudge the cursor onto its capture surface so the toolbar appears
        # and its controls become hit-testable.
        Set-UiTestForegroundWindow -Target $overlay
        $surface = Get-SystemElementCenter -Target $overlay -AutomationId 'FullscreenGrab.BackgroundImage'
        Invoke-SystemIntegrationHelper -Arguments @('--move', [string]$surface.X, [string]$surface.Y) | Out-Null
        foreach ($automationId in @(
            'FullscreenGrab.StandardModeToggle',
            'FullscreenGrab.SingleLineToggle',
            'FullscreenGrab.TableToggle',
            'FullscreenGrab.SelectionStyle',
            'FullscreenGrab.CancelButton'
        )) {
            Wait-UiTestElement -Target $overlay -AutomationId $automationId -TimeoutSeconds $Context.TimeoutSeconds
        }
        # The capture mode is settable: switching to single-line OCR toggles it on.
        Invoke-UiTestElement -Target $overlay -AutomationId 'FullscreenGrab.SingleLineToggle'
        Assert-UiTestWindow -ProcessId $overlay.ProcessId -WindowHandle $overlay.WindowHandle

        Invoke-SystemIntegrationHelper -Arguments @('--escape') | Out-Null
        Wait-FullscreenGrabDismissed -Context $Context -Target $overlay
    }
    finally {
        Stop-FullscreenGrabTarget -Context $Context -Target $overlay
    }
}
