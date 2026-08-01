Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

Register-UiTest -Suite 'GrabFrame' -Name 'image-file-route-opens-grab-frame-with-deterministic-controls' -Script {
    param($Context)

    $image = Join-Path (Split-Path -Parent $PSScriptRoot) '..\Tests\Images\font_sample.png'
    $image = [IO.Path]::GetFullPath($image)
    Assert-UiTestFile -Path $image
    $grabFrame = Start-DeterministicTextGrab -Context $Context -Arguments @('--grabframe', $image) -WindowTitle 'Grab Frame'
    Wait-UiTestElement -Target $grabFrame -AutomationId 'GrabFrame.Image' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $grabFrame -AutomationId 'GrabFrame.Search' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestWindow -ProcessId $grabFrame.ProcessId -WindowHandle $grabFrame.WindowHandle
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'GrabFrame' -Name 'table-and-refresh-controls-are-addressable-for-file-backed-captures' -Script {
    param($Context)

    $image = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSScriptRoot) '..\Tests\Images\Table-Test.png'))
    $grabFrame = Start-DeterministicTextGrab -Context $Context -Arguments @('--grabframe', $image) -WindowTitle 'Grab Frame'
    Wait-UiTestElement -Target $grabFrame -AutomationId 'GrabFrame.TableToggle' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $grabFrame -AutomationId 'GrabFrame.TableToggle'
    Wait-UiTestElement -Target $grabFrame -AutomationId 'GrabFrame.RefreshButton' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $grabFrame -AutomationId 'GrabFrame.RefreshButton'
    Assert-UiTestWindow -ProcessId $grabFrame.ProcessId -WindowHandle $grabFrame.WindowHandle
}

Register-UiTest -Suite 'GrabFrame' -Name 'pdf-save-reopen-and-barcode-routing-need-file-picker-or-ocr-capability' -SkipReason 'PDF save/reopen and QR/barcode assertions require native picker support and machine OCR/barcode capability; they belong to the capability-labelled fixture lane.' -Script {
    param($Context)
}

Register-UiTest -Suite 'GrabFrame' -Name 'search-text-entry-requires-foreground-real-input' -SkipReason 'Grab Frame search is a custom WPF control without ValuePattern. Its only input route is foreground send-input, which is explicitly outside this deterministic non-global suite.' -Script {
    param($Context)
}
