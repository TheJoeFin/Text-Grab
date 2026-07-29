Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

Register-UiTest -Suite 'Settings' -Name 'settings-navigation-contract-is-present' -Script {
    param($Context)

    $settings = Start-DeterministicTextGrab -Context $Context -Arguments @('Settings') -WindowTitle 'Text Grab Settings'
    foreach ($navigationId in @(
            'Settings.Nav.General', 'Settings.Nav.FullscreenGrab', 'Settings.Nav.GrabFrame',
            'Settings.Nav.QuickLookup', 'Settings.Nav.EditText', 'Settings.Nav.Languages',
            'Settings.Nav.Shortcuts', 'Settings.Nav.Tesseract', 'Settings.Nav.VoiceOutput',
            'Settings.Nav.Danger')) {
        Wait-UiTestElement -Target $settings -AutomationId $navigationId -TimeoutSeconds $Context.TimeoutSeconds
    }
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'Settings' -Name 'settings-page-navigation-and-persistence-need-dpi-correct-navigationview-clicks' -SkipReason 'On this machine WinApp CLI reports Wpf.Ui NavigationView item click coordinates outside the 660x700 Settings HWND (the same DPI-coordinate defect observed for native dialog buttons). InvokePattern is unavailable, so page activation cannot be asserted until the CLI coordinate mapping is corrected.' -Script {
    param($Context)

    $settings = Start-DeterministicTextGrab -Context $Context -Arguments @('Settings') -WindowTitle 'Text Grab Settings'
    Set-UiTestForegroundWindow -Target $settings
    Click-UiTestElement -Target $settings -AutomationId 'Settings.Nav.General'
    Wait-UiTestElement -Target $settings -AutomationId 'Settings.General.Theme.Dark' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $settings -AutomationId 'Settings.General.Theme.Dark'
    Click-UiTestElement -Target $settings -AutomationId 'Settings.Nav.EditText'
    Wait-UiTestElement -Target $settings -AutomationId 'Settings.EditText.SpellCheck.Off' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $settings -AutomationId 'Settings.EditText.SpellCheck.Off'
    Assert-UiTestFile -Path (Join-Path $Context.ProfileDirectory 'settings\classic-settings.json')
}

Register-UiTest -Suite 'Settings' -Name 'import-export-and-reset-use-native-pickers-or-confirmations' -SkipReason 'Import/export and destructive reset require native picker/confirmation automation; covered in the disposable interactive-desktop lane to prevent profile loss.' -Script {
    param($Context)
}
