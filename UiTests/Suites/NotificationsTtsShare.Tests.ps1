Import-Module "$PSScriptRoot\Capability.Helpers.psm1" -Force -Global -DisableNameChecking

Register-UiTest -Suite 'NotificationsTtsShare' -Name 'tts-engine-start-stop-and-toggle-contract' -RequiredCapabilities @('tts') -Script {
    param($Context)

    Invoke-CapabilityUnitTests -Filter 'FullyQualifiedName~TtsServiceTests|FullyQualifiedName~GrabFrameTtsTests'
    $image = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Tests\Images\font_sample.png'))
    $exe = (Get-Process -Id $Context.TextGrab.ProcessId -ErrorAction Stop).Path
    $frame = Start-UiTestProcess -Context $Context -FilePath $exe -Arguments @('--automation-profile', $Context.ProfileDirectory, '--grabframe', $image) -Kind TextGrab -WindowTitle 'Grab Frame'
    Wait-UiTestElement -Target $frame -AutomationId 'GrabFrame.SpeakToggle' -TimeoutSeconds $Context.TimeoutSeconds
    Invoke-UiTestElement -Target $frame -AutomationId 'GrabFrame.SpeakToggle'
    Invoke-UiTestElement -Target $frame -AutomationId 'GrabFrame.SpeakToggle'
    Save-UiTestScreenshot -Context $Context -Target $frame -Name 'tts-start-stop-toggle' | Out-Null
}

Register-UiTest -Suite 'NotificationsTtsShare' -Name 'voice-configuration-and-preview-contract-is-present' -RequiredCapabilities @('tts') -Script {
    param($Context)

    $voicePage = Join-Path $PSScriptRoot '..\..\Text-Grab\Pages\VoiceOutputSettings.xaml'
    $content = Get-Content -LiteralPath $voicePage -Raw
    foreach ($automationId in @('Settings.VoiceOutput.Voice', 'Settings.VoiceOutput.Rate', 'Settings.VoiceOutput.PreviewButton')) {
        Assert-CapabilityContains -Actual $content -Expected $automationId -Description 'voice settings automation contract'
    }
}

Register-UiTest -Suite 'NotificationsTtsShare' -Name 'notification-creation-and-click-routing-requires-notification-capability' -RequiredCapabilities @('notifications') -Script {
    param($Context)

    $notificationSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\Text-Grab\Utilities\NotificationUtilities.cs') -Raw
    $activatorSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\Text-Grab\TextGrabNotificationActivator.cs') -Raw
    Assert-CapabilityContains -Actual $notificationSource -Expected 'toast.Show()' -Description 'notification creation route'
    Assert-CapabilityContains -Actual $activatorSource -Expected 'EditTextWindow' -Description 'notification click route'
}

Register-UiTest -Suite 'NotificationsTtsShare' -Name 'share-target-activation-requires-registered-packaged-share-capability' -RequiredCapabilities @('shareTarget') -Script {
    param($Context)

    $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\Text-Grab\Utilities\ShareTargetUtilities.cs') -Raw
    foreach ($method in @('HandleSharedStorageItemsAsync', 'HandleSharedBitmapAsync', 'HandleSharedTextAsync', 'HandleSharedUriAsync')) {
        Assert-CapabilityContains -Actual $source -Expected $method -Description 'share-target activation route'
    }
}

Register-UiTest -Suite 'NotificationsTtsShare' -Name 'audible-voice-quality-remains-manual' -SkipReason 'Audio intelligibility, voice quality, and volume are subjective observations; this lane only verifies start/stop and configured-voice mechanics.' -Script {
    param($Context)
}
