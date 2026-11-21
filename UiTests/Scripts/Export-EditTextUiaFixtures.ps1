<#
.SYNOPSIS
Dumps the UI Automation tree of the Edit Text Window's editor area and calc pane, and the keyboard
order through them, in all four editor modes with the calc pane closed and open.

.DESCRIPTION
Each calc state runs in its own process with a fresh automation profile (the calc toggle saves its
state to the profile). In each process the window goes through Text, Markdown, Spreadsheet and Code
mode in that order. For each mode it writes two files to -OutputDirectory:

  <mode>.calc-<closed|open>.uia.txt  The window's children between the calc toggle (or the Markdown
                                     formatting bar) and the bottom bar, as Format-UiTestTree lines.
  <mode>.calc-<closed|open>.tab.txt  Focus stops: Tab from the calc toggle until an editor takes
                                     focus, then Ctrl+Tab from that editor.

The committed fixtures (UiTests/Fixtures/EditTextUia) were written by this script against the
hand-written window. The parity test (v5 E4a step 7) runs it again with the pane layout on and
compares. Sends real keys for the focus order: the windows it opens take focus.

.EXAMPLE
pwsh -NoProfile -File UiTests\Scripts\Export-EditTextUiaFixtures.ps1 -TextGrabPath Text-Grab\bin\x64\Debug\net10.0-windows10.0.22621.0\Text-Grab.exe -OutputDirectory UiTests\Fixtures\EditTextUia
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TextGrabPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [hashtable]$Settings = @{},
    # Seeds the EtwPaneLayout setting (v5 E4a); usable from -File, where a hashtable is not.
    [ValidateSet('', 'On', 'Off')][string]$PaneLayout = '',
    [int]$Depth = 12,
    [ValidateRange(5, 300)][int]$TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$uiTestsRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $uiTestsRoot 'TextGrab.UiTestHarness.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $uiTestsRoot 'Suites\Deterministic.Helpers.psm1') -Force -DisableNameChecking

if ($PaneLayout) { $Settings['EtwPaneLayout'] = $PaneLayout }

$textGrabExe = [IO.Path]::GetFullPath($TextGrabPath)
if (-not (Test-Path -LiteralPath $textGrabExe -PathType Leaf)) { throw "Text Grab executable was not found: $textGrabExe" }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
[void](Test-WinAppCli)

$modes = @(
    [pscustomobject]@{ Name = 'text'; MenuItem = 'EditText.Menu.RawTextMode'; Editor = 'EditText.Editor' }
    [pscustomobject]@{ Name = 'markdown'; MenuItem = 'EditText.Menu.MarkdownMode'; Editor = 'EditText.MarkdownEditor' }
    [pscustomobject]@{ Name = 'spreadsheet'; MenuItem = 'EditText.Menu.SpreadsheetMode'; Editor = 'EditText.SpreadsheetGrid' }
    [pscustomobject]@{ Name = 'code'; MenuItem = 'EditText.Menu.CodeMode'; Editor = 'EditText.CodeEditor' }
)
# Where Tab lands in each editor: the text boxes themselves, a grid cell, or the code editor's text
# area, which UI Automation reports as a bare Document.
$editorStops = @(
    '*#EditText.Editor (*', '*#EditText.MarkdownEditor (*', '*(DataGridCell)*', 'Document'
)
$sampleText = "1 + 1`r`n2 * 3"

$context = New-UiTestRunContext -ArtifactRoot (Join-Path $uiTestsRoot 'artifacts') -TimeoutSeconds $TimeoutSeconds
try {
    foreach ($calcOpen in @($false, $true)) {
        $state = if ($calcOpen) { 'open' } else { 'closed' }
        $profile = New-DeterministicSeedProfile -Context $context -Name "uia-calc-$state" -Settings $Settings
        $editText = Start-UiTestProcess -Context $context -FilePath $textGrabExe `
            -Arguments @('--automation-profile', $profile, 'EditText') -Kind TextGrab -WindowTitle 'Edit Text'
        Wait-UiTestDiagnosticEvent -Path (Join-Path $profile 'diagnostics\events.jsonl') -EventName 'ready' -TimeoutSeconds $TimeoutSeconds
        Set-UiTestValue -Target $editText -AutomationId 'EditText.Editor' -Value $sampleText

        if ($calcOpen) {
            Invoke-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorToggle'
            Wait-UiTestElement -Target $editText -AutomationId 'EditText.CalculatorResults' -Value '2' -Contains -TimeoutSeconds $TimeoutSeconds
        }

        foreach ($mode in $modes) {
            if ($mode.Name -ne 'text') {
                Open-DeterministicMenu -Target $editText -MenuAutomationId 'EditText.Menu.Format' -ChildAutomationId $mode.MenuItem -Context $context
                Invoke-UiTestElement -Target $editText -AutomationId $mode.MenuItem
            }
            Wait-UiTestElement -Target $editText -AutomationId $mode.Editor -TimeoutSeconds $TimeoutSeconds
            Start-Sleep -Milliseconds 700

            $window = @(Get-UiTestTree -Target $editText -Depth $Depth)[0]
            $slice = @(Select-UiTestTreeSlice -Node $window -After @('EditText.CalculatorToggle', 'EditText.MarkdownToolbar') -Before 'BottomBarScrollViewer')
            $uiaPath = Join-Path $outputRoot "$($mode.Name).calc-$state.uia.txt"
            @(Format-UiTestTree -Nodes $slice) | Set-Content -LiteralPath $uiaPath -Encoding utf8

            # Ctrl+Tab continues from wherever Tab put focus in the editor.
            $tabStops = @(Get-UiTestTabOrder -Target $editText -StartAutomationId 'EditText.CalculatorToggle' -Count 16 -StopAt $editorStops)
            $controlTabStops = @(Get-UiTestTabOrder -Target $editText -Count 6 -Key 'ctrl+tab')
            $tabPath = Join-Path $outputRoot "$($mode.Name).calc-$state.tab.txt"
            @('# Tab from the calc toggle') + $tabStops + @('# Ctrl+Tab from there') + $controlTabStops |
                Set-Content -LiteralPath $tabPath -Encoding utf8
            Write-Output "Wrote $uiaPath"
        }

        Stop-UiTestProcesses -Context $context
        $context.Processes.Clear()
    }
}
finally {
    Stop-UiTestProcesses -Context $context
}
