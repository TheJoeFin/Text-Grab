Register-UiTest -Suite 'Harness' -Name 'fixture-ui-value-and-cleanup-contract' -Script {
    param($Context)

    Assert-UiTestProcess -ProcessId $Context.Fixture.ProcessId
    Assert-UiTestWindow -ProcessId $Context.Fixture.ProcessId -WindowHandle $Context.Fixture.WindowHandle
    Assert-UiTestFile -Path $Context.FixtureStatePath
    Wait-UiTestElement -Target $Context.Fixture -AutomationId 'PostGrabInputTarget' -TimeoutSeconds $Context.TimeoutSeconds

    $expected = "Harness value $($Context.RunId)"
    Set-UiTestValue -Target $Context.Fixture -AutomationId 'PostGrabInputTarget' -Value $expected
    Wait-UiTestElement -Target $Context.Fixture -AutomationId 'ReceivedInputText' -Value $expected -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestValue -Target $Context.Fixture -AutomationId 'ReceivedInputText' -Expected $expected

    Assert-UiTestFile -Path $Context.SeedPath
    Assert-UiTestFile -Path (Join-Path $Context.ProfileDirectory 'seed.json')
    if ($Context.ProfileDirectory -like "$env:APPDATA*" -or $Context.ProfileDirectory -like "$env:LOCALAPPDATA*") {
        throw "Automation profile must be isolated from the user profile: $($Context.ProfileDirectory)"
    }
    Assert-UiTestUserProfileUnchanged -Context $Context
    Save-UiTestScreenshot -Context $Context -Target $Context.Fixture -Name 'harness-fixture' | Out-Null
}
