Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

Register-UiTest -Suite 'QuickLookup' -Name 'lookup-window-and-copy-controls-are-uia-addressable' -Script {
    param($Context)

    $lookup = Start-DeterministicTextGrab -Context $Context -Arguments @('QuickLookup') -WindowTitle 'Quick Simple Lookup'
    Wait-UiTestElement -Target $lookup -AutomationId 'QuickLookup.Search' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $lookup -AutomationId 'QuickLookup.ResultsGrid' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $lookup -AutomationId 'QuickLookup.CopySelectedButton' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'QuickLookup' -Name 'lookup-filter-text-entry-uses-targeted-foreground-input' -Script {
    param($Context)

    $lookup = Start-DeterministicTextGrab -Context $Context -Arguments @('QuickLookup') -WindowTitle 'Quick Simple Lookup'
    Wait-UiTestElement -Target $lookup -AutomationId 'QuickLookup.Search' -TimeoutSeconds $Context.TimeoutSeconds
    Set-DeterministicInteractiveValue -Target $lookup -AutomationId 'QuickLookup.Search' -Value 'fixture'
    Wait-UiTestElement -Target $lookup -AutomationId 'QuickLookup.ResultCount' -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'QuickLookup' -Name 'lookup-crud-import-export-needs-datagrid-row-and-native-file-dialog-driving' -SkipReason 'Import/export still requires a native file picker and deterministic DataGrid cell automation has not yet been added to this suite.' -Script {
    param($Context)
}
