Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

Register-UiTest -Suite 'CliProtocol' -Name 'cli-modes-route-to-their-own-window-contracts' -Script {
    param($Context)

    $modes = @(
        @{ Argument = 'EditText'; Title = 'Edit Text'; Element = 'EditText.Editor' },
        @{ Argument = 'QuickLookup'; Title = 'Quick Simple Lookup'; Element = 'QuickLookup.Search' },
        @{ Argument = 'Settings'; Title = 'Text Grab Settings'; Element = 'Settings.Nav.General' }
    )
    foreach ($mode in $modes) {
        $target = Start-DeterministicTextGrab -Context $Context -Arguments @($mode.Argument) -WindowTitle $mode.Title
        Wait-UiTestElement -Target $target -AutomationId $mode.Element -TimeoutSeconds $Context.TimeoutSeconds
    }
}

Register-UiTest -Suite 'CliProtocol' -Name 'grabframe-flag-and-safe-protocol-activation-route-a-local-fixture' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'protocol'
    $temp = Join-Path $profile 'temp'
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    $fixture = Join-Path $temp 'protocol-fixture.png'
    Copy-Item -LiteralPath ([IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSScriptRoot) '..\Tests\Images\font_sample.png'))) -Destination $fixture
    $uri = "text-grab://grab-frame?path=$([Uri]::EscapeDataString($fixture))"

    $target = Start-DeterministicTextGrab -Context $Context -ProfileDirectory $profile -Arguments @($uri) -WindowTitle 'Grab Frame'
    Wait-UiTestElement -Target $target -AutomationId 'GrabFrame.Image' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path $fixture
    Assert-UiTestUserProfileUnchanged -Context $Context
}

Register-UiTest -Suite 'CliProtocol' -Name 'windowless-folder-routing-and-bulk-output-require-ocr-and-shell-capabilities' -SkipReason 'Windowless OCR/folder bulk output has no UI target and depends on installed OCR language packs and clipboard/shell output; it is exercised by the capability-labelled CLI lane.' -Script {
    param($Context)
}
