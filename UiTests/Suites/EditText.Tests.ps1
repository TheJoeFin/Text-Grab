Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

Register-UiTest -Suite 'EditText' -Name 'editor-value-and-single-line-transform-are-uia-driven' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value "alpha`r`nbeta"
    Assert-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Expected "alpha`r`nbeta"
    Open-DeterministicMenu -Target $editText -MenuAutomationId 'EditText.Menu.Edit' -ChildAutomationId 'EditText.Menu.SingleLine' -Context $Context
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.Menu.SingleLine'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'alpha' -Contains -TimeoutSeconds $Context.TimeoutSeconds
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'EditText' -Name 'find-replace-dialog-replaces-editor-content' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value 'before before'
    Open-DeterministicMenu -Target $editText -MenuAutomationId 'EditText.Menu.Selection' -ChildAutomationId 'EditText.Menu.FindAndReplace' -Context $Context
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.Menu.FindAndReplace'
    $dialog = Wait-UiTestDialog -ProcessId $editText.ProcessId -Title 'Find and Replace' -TimeoutSeconds $Context.TimeoutSeconds
    Set-DeterministicInteractiveValue -Target $dialog -AutomationId 'FindReplace.Search' -Value 'before'
    Set-DeterministicInteractiveValue -Target $dialog -AutomationId 'FindReplace.ReplaceText' -Value 'after'
    Invoke-UiTestElement -Target $dialog -AutomationId 'FindReplace.ReplaceAllButton'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'after after' -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'EditText' -Name 'calculator-pane-surface-can-be-entered' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value '1 + 1'
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'EditText' -Name 'spreadsheet-and-markdown-modes-are-opened-from-stable-parent-menus' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value "name`tvalue`r`nalpha`t1"
    Open-DeterministicMenu -Target $editText -MenuAutomationId 'EditText.Menu.Format' -ChildAutomationId 'EditText.Menu.SpreadsheetMode' -Context $Context
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.Menu.SpreadsheetMode'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.SpreadsheetGrid' -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'EditText' -Name 'native-open-and-save-as-dialogs-use-dedicated-hwnd-targets' -SkipReason 'The Windows common dialog is discoverable and its filename ComboBox is settable, but this machine exposes its Open/Save command as a non-HWND UIA element: InvokePattern, click, Enter, and BM_CLICK do not close it. Keep the dialog-helper contract; execute outcome coverage on a machine whose common dialog exposes an invokable command.' -Script {
    param($Context)

    $inputPath = Join-Path $Context.OutputDirectory 'edit-input.txt'
    $savePath = Join-Path $Context.OutputDirectory 'edit-saved.txt'
    'opened from a deterministic native dialog' | Set-Content -LiteralPath $inputPath -Encoding utf8

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    Open-DeterministicMenu -Target $editText -MenuAutomationId 'EditText.Menu.File' -ChildAutomationId 'EditText.Menu.OpenFile' -Context $Context
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.Menu.OpenFile'
    $openDialog = Wait-UiTestDialog -ProcessId $editText.ProcessId -Title 'Open' -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $openDialog -AutomationId 'File name:' -Value $inputPath
    Invoke-UiTestNativeDialogButton -Target $openDialog -Caption 'Open'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'opened from a deterministic native dialog' -Contains -TimeoutSeconds $Context.TimeoutSeconds

    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value 'saved through a deterministic native dialog'
    Open-DeterministicMenu -Target $editText -MenuAutomationId 'EditText.Menu.File' -ChildAutomationId 'EditText.Menu.SaveAs' -Context $Context
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.Menu.SaveAs'
    $saveDialog = Wait-UiTestDialog -ProcessId $editText.ProcessId -Title 'Save As' -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $saveDialog -AutomationId 'File name:' -Value $savePath
    Invoke-UiTestNativeDialogButton -Target $saveDialog -Caption 'Save'
    Assert-UiTestFile -Path $savePath
    if ((Get-Content -LiteralPath $savePath -Raw) -notmatch 'saved through a deterministic native dialog') {
        throw "Save As output did not contain the expected editor content: $savePath"
    }
}

Register-UiTest -Suite 'EditText' -Name 'clipboard-watcher-remains-system-integration-only' -SkipReason 'Clipboard watcher behavior depends on an external clipboard change and is owned by the system-integration suite.' -Script {
    param($Context)
}
