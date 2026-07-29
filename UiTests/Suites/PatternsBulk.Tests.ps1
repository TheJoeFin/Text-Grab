Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

Register-UiTest -Suite 'PatternsBulk' -Name 'pattern-and-bulk-fixtures-are-available-under-the-repository' -Script {
    param($Context)

    foreach ($fixture in @(
            (Join-Path (Split-Path -Parent $PSScriptRoot) '..\Tests\Images\font_sample.png'),
            (Join-Path (Split-Path -Parent $PSScriptRoot) '..\Tests\Images\Table-Test.png'),
            (Join-Path (Split-Path -Parent $PSScriptRoot) '..\Tests\Images\QrCodeTestImage.png'))) {
        Assert-UiTestFile -Path ([IO.Path]::GetFullPath($fixture))
    }
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'PatternsBulk' -Name 'pattern-regex-template-and-folder-flows-need-nested-menu-or-folder-picker-support' -SkipReason 'Patterns/templates are opened from WPF nested menus and bulk-folder flow requires a native folder picker. WinApp CLI v0.5 exposes neither deterministic parent-menu expansion nor folder-dialog driving, so these scenarios are explicitly deferred rather than silently passed.' -Script {
    param($Context)
}
