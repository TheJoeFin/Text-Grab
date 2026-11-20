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

Register-UiTest -Suite 'EditText' -Name 'command-palette-runs-legacy-and-registered-editor-actions' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    $search = 'EditText.CommandPalette.Search'
    $text = "first`r`nsecond"
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value $text
    Send-UiTestKeys -Target $editText -AutomationId 'EditText.Editor' -Keys 'ctrl+home ctrl+shift+p' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $editText -AutomationId $search -Value 'selection isolate'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CommandPalette.NoResults' -TimeoutSeconds $Context.TimeoutSeconds
    Send-UiTestKeys -Target $editText -Keys 'esc' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Expected $text

    Send-UiTestKeys -Target $editText -Keys 'shift+right shift+right ctrl+shift+p' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $editText -AutomationId $search -Value 'selection isolate'
    Send-UiTestKeys -Target $editText -Keys 'enter' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'fi' -TimeoutSeconds $Context.TimeoutSeconds

    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value $text
    Send-UiTestKeys -Target $editText -Keys 'ctrl+shift+p' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $editText -AutomationId $search -Value 'lines make single'
    Send-UiTestKeys -Target $editText -Keys 'enter' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'first second' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'EditText' -Name 'command-palette-opens-in-code-mode-and-runs-click-handlers' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    $search = 'EditText.CommandPalette.Search'
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value "  alpha  "
    Send-UiTestKeys -Target $editText -AutomationId 'EditText.Editor' -Keys 'ctrl+shift+p' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $editText -AutomationId $search -Value 'code'
    Send-UiTestKeys -Target $editText -Keys 'enter' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CodeEditor' -TimeoutSeconds $Context.TimeoutSeconds

    Send-UiTestKeys -Target $editText -Keys 'ctrl+shift+p' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $editText -AutomationId $search -Value 'code language java'
    Send-UiTestKeys -Target $editText -Keys 'down enter' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.StatusText' -Value 'JavaScript' -Contains -TimeoutSeconds $Context.TimeoutSeconds

    Send-UiTestKeys -Target $editText -Keys 'ctrl+shift+p' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $editText -AutomationId $search -Value 'trim each line'
    Send-UiTestKeys -Target $editText -Keys 'enter' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -Gone -TimeoutSeconds $Context.TimeoutSeconds

    Send-UiTestKeys -Target $editText -Keys 'ctrl+shift+p' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId $search -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $editText -AutomationId $search -Value 'raw text'
    Send-UiTestKeys -Target $editText -Keys 'enter' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value "alpha`r`n" -TimeoutSeconds $Context.TimeoutSeconds
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'EditText' -Name 'find-replace-dialog-replaces-editor-content' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value 'before before'
    Open-DeterministicMenu -Target $editText -MenuAutomationId 'EditText.Menu.Selection' -ChildAutomationId 'EditText.Menu.FindAndReplace' -Context $Context
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.Menu.FindAndReplace'
    $dialog = Wait-UiTestDialog -ProcessId $editText.ProcessId -Title 'Find and Replace' -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestValue -Target $dialog -AutomationId 'InnerTextBox' -Value 'before'
    Set-UiTestValue -Target $dialog -AutomationId 'FindReplace.ReplaceText' -Value 'after'
    Invoke-UiTestElement -Target $dialog -AutomationId 'FindReplace.FindButton'
    Wait-UiTestElement -Target $dialog -AutomationId 'MatchesText' -Value '2 Matches' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $dialog -AutomationId 'FindReplace.ReplaceAllButton'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'after after' -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'EditText' -Name 'calculator-pane-surface-can-be-entered' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value '1 + 1'
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -TimeoutSeconds $Context.TimeoutSeconds
    # The pane evaluates after its typing delay.
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Value '2' -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'EditText' -Name 'pane-layout-calculator-pane-opens-shows-a-result-and-hides' -Script {
    param($Context)

    # v5 E4a: the editor area rendered from classic.json through AvalonDock.
    $profile = New-DeterministicSeedProfile -Context $Context -Name 'pane-layout-on' -Settings @{ EtwPaneLayout = 'On' }
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value '1 + 1'
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Value '2' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'TextBoxSplitter' -TimeoutSeconds $Context.TimeoutSeconds

    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorCopyAllButton' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'TextBoxSplitter' -Gone -TimeoutSeconds $Context.TimeoutSeconds

    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Value '2' -TimeoutSeconds $Context.TimeoutSeconds
    $failure = Join-Path $profile 'diagnostics\failure.json'
    Assert-UiTestFile -Path $failure -Absent
}

Register-UiTest -Suite 'EditText' -Name 'pane-layout-calculator-toggle-leaves-focus-in-the-editor' -Script {
    param($Context)

    # Showing an AvalonDock anchorable makes it the active content, which moves focus into it unless the host puts focus back.
    $profile = New-DeterministicSeedProfile -Context $Context -Name 'pane-layout-focus' -Settings @{ EtwPaneLayout = 'On' }
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value '2 * 3'
    Set-UiTestForegroundWindow -Target $editText

    # Visit the results first, so AvalonDock remembers them as the pane's last focused element.
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Value '6' -TimeoutSeconds $Context.TimeoutSeconds
    Focus-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults'
    Focus-UiTestElement -Target $editText -AutomationId 'EditText.Editor'

    foreach ($expectResults in @($false, $true, $false)) {
        Send-UiTestKeys -Target $editText -AutomationId 'EditText.Editor' -Keys 'ctrl+p' -Via send-input
        if ($expectResults) {
            Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -TimeoutSeconds $Context.TimeoutSeconds
        }
        else {
            Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Gone -TimeoutSeconds $Context.TimeoutSeconds
        }
        Start-Sleep -Milliseconds 300
        $focused = Get-UiTestFocusedElement -Target $editText
        if ($focused -notlike '*#EditText.Editor *') {
            throw "After Ctrl+P (results shown: $expectResults) focus was on '$focused', not the editor."
        }
    }
}

Register-UiTest -Suite 'EditText' -Name 'pane-layout-editor-area-matches-the-hand-written-fixtures' -Script {
    param($Context)

    # v5 E4a step 7. The capture opens its own windows and sends Tab keys, so it runs in a child process
    # with its own module state. EditTextUiaParity.psm1 lists what may differ, and why.
    $suiteRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Join-Path (Get-Location) 'UiTests\Suites' }
    Import-Module (Join-Path $suiteRoot 'EditTextUiaParity.psm1') -Force
    $capture = Join-Path $Context.OutputDirectory 'uia-pane-layout'
    $exporter = Join-Path $suiteRoot '..\Scripts\Export-EditTextUiaFixtures.ps1'
    $shell = (Get-Process -Id $PID).Path
    & $shell -NoProfile -File $exporter -TextGrabPath (Get-DeterministicTextGrabExecutable $Context) -OutputDirectory $capture -PaneLayout On -TimeoutSeconds $Context.TimeoutSeconds | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "The UI Automation capture failed with exit code $LASTEXITCODE." }

    $problems = @(Compare-EditTextUiaParity -FixtureDirectory (Join-Path $suiteRoot '..\Fixtures\EditTextUia') -ActualDirectory $capture)
    if ($problems.Count -gt 0) {
        throw "The pane layout's editor area differs from the hand-written window's:`n$($problems -join "`n`n")"
    }
}

Register-UiTest -Suite 'EditText' -Name 'pane-layout-a-layout-file-in-the-store-opens-with-the-layout-argument' -Script {
    param($Context)

    # v5 E4a "Done when": a second layout file dropped in the store opens with --layout. This one puts the
    # calc pane on the editor's left, which Classic never does.
    $profile = New-DeterministicSeedProfile -Context $Context -Name 'pane-layout-store' -Settings @{ EtwPaneLayout = 'On' }
    $layouts = Join-Path $profile 'data\layouts'
    New-Item -ItemType Directory -Path $layouts -Force | Out-Null
    @'
{
  "schemaVersion": "1.0",
  "id": "calc-left",
  "name": "Calc on the left",
  "root": {
    "type": "split",
    "orientation": "horizontal",
    "children": [
      { "node": { "type": "pane", "paneId": "textgrab.pane.calc" }, "size": 220, "unit": "pixel" },
      { "node": { "type": "pane", "paneId": "textgrab.pane.editor" }, "size": 1, "unit": "star" }
    ]
  }
}
'@ | Set-Content -LiteralPath (Join-Path $layouts 'calc-left.json') -Encoding utf8

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('--layout', 'calc-left') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value '1 + 1'
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Value '2' -TimeoutSeconds $Context.TimeoutSeconds

    $results = (Invoke-UiTestWinApp -Arguments @('ui', 'inspect', 'EditText.CalculatorResults', '--window', [string]$editText.WindowHandle, '--depth', '0') -Json).windows[0].elements[0]
    $editor = (Invoke-UiTestWinApp -Arguments @('ui', 'inspect', 'EditText.Editor', '--window', [string]$editText.WindowHandle, '--depth', '0') -Json).windows[0].elements[0]
    if ([int]$results.x -ge [int]$editor.x) {
        throw "With --layout calc-left the calc results should be left of the editor; results x=$($results.x), editor x=$($editor.x)."
    }
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'pane-layout-an-unknown-layout-opens-classic-and-says-why' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'pane-layout-unknown' -Settings @{ EtwPaneLayout = 'On' }
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('--layout', 'no-such-layout') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestDiagnosticEvent -Path (Join-Path $profile 'diagnostics\events.jsonl') -EventName 'layout-notice' -TimeoutSeconds $Context.TimeoutSeconds
    $notice = Get-Content -LiteralPath (Join-Path $profile 'diagnostics\events.jsonl') | ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object eventName -eq 'layout-notice' | Select-Object -First 1
    if ([string]$notice.details.notice -notlike '*no-such-layout*Classic*') {
        throw "The notice should name the layout and say Classic opened instead; it was '$($notice.details.notice)'."
    }

    # Classic: the calc pane still opens, to the editor's right.
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value '1 + 1'
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Value '2' -TimeoutSeconds $Context.TimeoutSeconds
}

Register-UiTest -Suite 'EditText' -Name 'hand-written-grid-calculator-pane-still-opens' -Script {
    param($Context)

    # The way back for testers in 4.17: EtwPaneLayout = Off keeps the 4.16 editor grid.
    $profile = New-DeterministicSeedProfile -Context $Context -Name 'pane-layout-off' -Settings @{ EtwPaneLayout = 'Off' }
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value '1 + 1'
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Value '2' -TimeoutSeconds $Context.TimeoutSeconds
    $splitterClass = Get-UiTestProperty -Target $editText -AutomationId 'TextBoxSplitter' -Property 'ClassName'
    if ($splitterClass -ne 'GridSplitter') {
        throw "Expected the hand-written GridSplitter with EtwPaneLayout = Off; found '$splitterClass'."
    }
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

Register-UiTest -Suite 'EditText' -Name 'recent-pane-explorer-layout-lists-seeded-items-and-opens-one-with-enter' -Script {
    param($Context)

    # v5 E5 recent: the Explorer layout shows the recent items beside the editor; Enter in the search box opens the first.
    $profile = New-DeterministicSeedProfile -Context $Context -Name 'recent-enter' -Settings @{ EtwPaneLayout = 'On' }
    Set-DeterministicTextHistory -ProfileDirectory $profile -Texts @('Recent pane seeded note')
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('--layout', 'explorer') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Recent.Items' -TimeoutSeconds $Context.TimeoutSeconds
    # Rows are UIA list items named "Edit Text: <text>"; a row's AutomationId does not reach UIA, and
    # 'EditText.Recent.Item' would only match the list 'EditText.Recent.Items' by prefix.
    Wait-UiTestElement -Target $editText -AutomationId 'Edit Text: Recent pane seeded note' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Recent.EmptyText' -Gone -TimeoutSeconds $Context.TimeoutSeconds

    $recent = (Invoke-UiTestWinApp -Arguments @('ui', 'inspect', 'EditText.Recent.Items', '--window', [string]$editText.WindowHandle, '--depth', '0') -Json).windows[0].elements[0]
    $editor = (Invoke-UiTestWinApp -Arguments @('ui', 'inspect', 'EditText.Editor', '--window', [string]$editText.WindowHandle, '--depth', '0') -Json).windows[0].elements[0]
    if ([int]$recent.x -ge [int]$editor.x) {
        throw "With --layout explorer the recent items should be left of the editor; recent x=$($recent.x), editor x=$($editor.x)."
    }

    Set-UiTestForegroundWindow -Target $editText
    Focus-UiTestElement -Target $editText -AutomationId 'EditText.Recent.Search'
    Send-UiTestKeys -Target $editText -AutomationId 'EditText.Recent.Search' -Keys 'enter' -Via send-input
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'Recent pane seeded note' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'recent-pane-double-click-opens-an-item-in-the-blank-window' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'recent-double-click' -Settings @{ EtwPaneLayout = 'On' }
    Set-DeterministicTextHistory -ProfileDirectory $profile -Texts @('Opened by a double-click')
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('--layout', 'explorer') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'Edit Text: Opened by a double-click' -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestForegroundWindow -Target $editText
    Click-UiTestElement -Target $editText -AutomationId 'Edit Text: Opened by a double-click' -Double
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'Opened by a double-click' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'recent-pane-search-narrows-the-list-and-says-when-nothing-matches' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'recent-search' -Settings @{ EtwPaneLayout = 'On' }
    Set-DeterministicTextHistory -ProfileDirectory $profile -Texts @('alpha note', 'beta note')
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('--layout', 'explorer') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'Edit Text: alpha note' -TimeoutSeconds $Context.TimeoutSeconds

    Set-UiTestForegroundWindow -Target $editText
    Set-UiTestValue -Target $editText -AutomationId 'EditText.Recent.Search' -Value 'zzz'
    Wait-UiTestElement -Target $editText -AutomationId 'Edit Text: alpha note' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Recent.EmptyText' -Value 'No matches' -TimeoutSeconds $Context.TimeoutSeconds

    Set-UiTestValue -Target $editText -AutomationId 'EditText.Recent.Search' -Value 'beta'
    Wait-UiTestElement -Target $editText -AutomationId 'Edit Text: beta note' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'Edit Text: alpha note' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Recent.EmptyText' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'recent-pane-is-not-in-the-classic-layout' -Script {
    param($Context)

    # A window that never asks for a layout is the 4.16 window: no recent pane, and it never read history for one.
    $profile = New-DeterministicSeedProfile -Context $Context -Name 'recent-classic' -Settings @{ EtwPaneLayout = 'On' }
    Set-DeterministicTextHistory -ProfileDirectory $profile -Texts @('Never shown in Classic')
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Recent.Items' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Recent.Search' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'mermaid-an-mmd-file-opens-the-mermaid-layout-in-code-mode-with-the-diagram-pane' -Script {
    param($Context)

    # v5 E5 mermaid: .mmd is in the built-in file-type map, so the file opens in the Mermaid layout, in Code mode
    # as Plain Text, with the diagram pane beside the editor and no calc pane.
    $profile = New-DeterministicSeedProfile -Context $Context -Name 'mermaid-mmd' -Settings @{ EtwPaneLayout = 'On' }
    $path = Copy-DeterministicMermaidFixture -ProfileDirectory $profile -Name 'flow.mmd'
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @($path) -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CodeEditor' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.StatusText' -Value 'Plain Text' -Contains -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Error' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-DeterministicLeftOf -Target $editText -Left 'EditText.CodeEditor' -Right 'EditText.Mermaid.Diagram'
    Wait-DeterministicProperty -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -Property 'HelpText' `
        -Predicate { param($value) $value -like 'flowchart LR*Draw it*' } -Description 'the diagram of the file' -TimeoutSeconds $Context.TimeoutSeconds | Out-Null
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'mermaid-editing-the-source-updates-the-diagram-and-a-broken-source-keeps-it-under-an-error' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'mermaid-edit' -Settings @{ EtwPaneLayout = 'On' }
    $path = Copy-DeterministicMermaidFixture -ProfileDirectory $profile -Name 'flow.mmd'
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @($path) -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -TimeoutSeconds $Context.TimeoutSeconds
    Set-UiTestForegroundWindow -Target $editText

    # The picture follows the source. The help text is the start of the source the picture was drawn from.
    Set-UiTestValue -Target $editText -AutomationId 'EditText.CodeEditor' -Value "flowchart LR`r`nX --> Y"
    Wait-DeterministicProperty -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -Property 'HelpText' `
        -Predicate { param($value) $value -like '*X --> Y*' } -Description 'the edited source to be drawn' -TimeoutSeconds $Context.TimeoutSeconds | Out-Null

    # A source that does not draw keeps the last good diagram and says why in the strip.
    Set-UiTestValue -Target $editText -AutomationId 'EditText.CodeEditor' -Value 'this is not a Mermaid diagram'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Error' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -TimeoutSeconds $Context.TimeoutSeconds
    $kept = Get-UiTestProperty -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -Property 'HelpText'
    if ($kept -notlike '*X --> Y*') {
        throw "A broken source should leave the last good diagram; its help text is now '$kept'."
    }

    # Fixing it draws again and clears the strip.
    Set-UiTestValue -Target $editText -AutomationId 'EditText.CodeEditor' -Value "flowchart LR`r`nP --> Q"
    Wait-DeterministicProperty -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -Property 'HelpText' `
        -Predicate { param($value) $value -like '*P --> Q*' } -Description 'the fixed source to be drawn' -TimeoutSeconds $Context.TimeoutSeconds | Out-Null
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Error' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'mermaid-layout-argument-opens-the-mermaid-layout-in-code-mode-on-a-blank-window' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'mermaid-layout-arg' -Settings @{ EtwPaneLayout = 'On' }
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('--layout', 'mermaid') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CodeEditor' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Placeholder' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.StatusText' -Value 'Plain Text' -Contains -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-DeterministicLeftOf -Target $editText -Left 'EditText.CodeEditor' -Right 'EditText.Mermaid.Placeholder'

    Set-UiTestForegroundWindow -Target $editText
    Set-UiTestValue -Target $editText -AutomationId 'EditText.CodeEditor' -Value "flowchart LR`r`nA --> B"
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Placeholder' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'mermaid-a-txt-file-still-opens-in-classic-without-the-diagram-pane' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'mermaid-txt' -Settings @{ EtwPaneLayout = 'On' }
    $path = Copy-DeterministicMermaidFixture -ProfileDirectory $profile -Name 'notes.txt'
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @($path) -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -Value 'Plain notes' -Contains -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CodeEditor' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Placeholder' -Gone -TimeoutSeconds $Context.TimeoutSeconds

    # Classic still has its calc pane.
    Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'mermaid-a-markdown-file-still-opens-in-markdown-mode-in-classic' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'mermaid-md' -Settings @{ EtwPaneLayout = 'On' }
    $path = Copy-DeterministicMermaidFixture -ProfileDirectory $profile -Name 'readme.md'
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @($path) -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.MarkdownEditor' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.CodeEditor' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Placeholder' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'mermaid-pane-is-not-in-the-classic-layout' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'mermaid-classic' -Settings @{ EtwPaneLayout = 'On' }
    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text' -ProfileDirectory $profile
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Placeholder' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Mermaid.Diagram' -Gone -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'diagnostics\failure.json') -Absent
}

Register-UiTest -Suite 'EditText' -Name 'clipboard-watcher-remains-system-integration-only' -SkipReason 'Clipboard watcher behavior depends on an external clipboard change and is owned by the system-integration suite.' -Script {
    param($Context)
}
