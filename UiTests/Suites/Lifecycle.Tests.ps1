Import-Module "$PSScriptRoot\Deterministic.Helpers.psm1" -Force -Global

Register-UiTest -Suite 'Lifecycle' -Name 'isolated-profile-default-launch-has-one-target-window' -Script {
    param($Context)

    $editText = Start-DeterministicTextGrab -Context $Context -Arguments @('EditText') -WindowTitle 'Edit Text'
    Assert-UiTestProcess -ProcessId $editText.ProcessId
    Assert-UiTestWindow -ProcessId $editText.ProcessId -WindowHandle $editText.WindowHandle
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path $Context.SeedPath
    Assert-DeterministicAutomationHealthy -Context $Context
}

Register-UiTest -Suite 'Lifecycle' -Name 'first-run-start-dismisses-into-selected-mode-with-isolated-profile' -Script {
    param($Context)

    $profile = New-DeterministicSeedProfile -Context $Context -Name 'first-run' -Settings @{ FirstRun = $true }
    $firstRun = Start-DeterministicTextGrab -Context $Context -ProfileDirectory $profile -Arguments @() -WindowTitle 'Welcome to Text Grab'
    Wait-UiTestElement -Target $firstRun -AutomationId 'FirstRun.DefaultEditTextRadio' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $firstRun -AutomationId 'FirstRun.DefaultEditTextRadio'
    Invoke-UiTestElement -Target $firstRun -AutomationId 'FirstRun.StartButton'

    $editText = New-DeterministicTarget -Process $firstRun -Title 'Edit Text' -Context $Context
    Wait-UiTestElement -Target $editText -AutomationId 'EditText.Editor' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestFile -Path (Join-Path $profile 'seed.json')
    Assert-UiTestUserProfileUnchanged -Context $Context
}
