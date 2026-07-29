Import-Module "$PSScriptRoot\Capability.Helpers.psm1" -Force -Global -DisableNameChecking

Register-UiTest -Suite 'OcrCapability' -Name 'winrt-ocr-font-table-and-qr-fixtures-match-unit-expectations' -RequiredCapabilities @('winrtOcr') -Script {
    param($Context)

    Invoke-CapabilityUnitTests -Filter 'FullyQualifiedName~OcrTests.OcrFontSampleImage|FullyQualifiedName~OcrTests.AnalyzeTable|FullyQualifiedName~OcrTests.ReadQrCode'
    $fixture = Start-CapabilityFixture -Context $Context -Surface 'OcrSamples'
    Wait-UiTestElement -Target $fixture -AutomationId 'OcrSampleImage' -TimeoutSeconds $Context.TimeoutSeconds
    Save-UiTestScreenshot -Context $Context -Target $fixture -Name 'ocr-winrt-fixtures' | Out-Null
}

Register-UiTest -Suite 'OcrCapability' -Name 'direct-text-uia-and-rtl-cjk-fixtures-are-addressable' -RequiredCapabilities @('interactiveDesktop') -Script {
    param($Context)

    $direct = Start-CapabilityFixture -Context $Context -Surface 'DirectText'
    Wait-UiTestElement -Target $direct -AutomationId 'DirectTextNativeValue' -TimeoutSeconds $Context.TimeoutSeconds
    Assert-UiTestValue -Target $direct -AutomationId 'DirectTextNativeValue' -Expected 'The quick brown fox jumps over the lazy dog.' -Contains
    $multilingual = Start-CapabilityFixture -Context $Context -Surface 'Multilingual'
    Wait-UiTestElement -Target $multilingual -AutomationId 'RightToLeftText' -TimeoutSeconds $Context.TimeoutSeconds
    Wait-UiTestElement -Target $multilingual -AutomationId 'MultilingualTextValue' -TimeoutSeconds $Context.TimeoutSeconds
    Save-UiTestScreenshot -Context $Context -Target $multilingual -Name 'ocr-direct-text-rtl-cjk' | Out-Null
}

Register-UiTest -Suite 'OcrCapability' -Name 'installed-cjk-language-pack-exercises-japanese-fixture' -RequiredCapabilities @('cjkOcr', 'interactiveDesktop') -Script {
    param($Context)

    $fixture = Start-CapabilityFixture -Context $Context -Surface 'OcrSamples'
    Wait-UiTestElement -Target $fixture -AutomationId 'OcrSampleSelector' -TimeoutSeconds $Context.TimeoutSeconds
    Save-UiTestScreenshot -Context $Context -Target $fixture -Name 'ocr-cjk-language-fixture' | Out-Null
}

Register-UiTest -Suite 'OcrCapability' -Name 'tesseract-font-fixture-has-normalized-expected-output' -RequiredCapabilities @('tesseract') -Script {
    param($Context)

    $tesseract = $Context.Capabilities.capabilities.tesseract.value
    if ($tesseract.languages -notcontains 'eng') {
        Skip-UiTest "Tesseract is installed but its English language data is absent. Installed languages: $($tesseract.languages -join ', ')."
    }
    $image = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Tests\Images\font_sample.png'))
    $stdout = Join-Path $Context.OutputDirectory 'tesseract-font-sample.stdout.txt'
    $stderr = Join-Path $Context.OutputDirectory 'tesseract-font-sample.stderr.txt'
    $process = Start-Process -FilePath $tesseract.path -ArgumentList @("`"$image`"", '-', '-l', 'eng') -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $actual = (Get-Content -LiteralPath $stdout -Raw).Trim() -replace '\r\n?', "`n"
    if ($process.ExitCode -ne 0) {
        throw "Tesseract exited with code $($process.ExitCode): $(Get-Content -LiteralPath $stderr -Raw)"
    }
    foreach ($expected in @('Times-Roman', 'Helvetica', 'Courier', 'Palatino-Roman', 'Bookman-Demi')) {
        Assert-CapabilityContains -Actual $actual -Expected $expected -Description 'normalized Tesseract output'
    }
}

Register-UiTest -Suite 'OcrCapability' -Name 'windows-ai-conditional-lane-is-only-enabled-on-supported-packaged-arm64' -RequiredCapabilities @('windowsAi') -Script {
    param($Context)

    $fixture = Start-CapabilityFixture -Context $Context -Surface 'OcrSamples'
    Wait-UiTestElement -Target $fixture -AutomationId 'OcrSampleImage' -TimeoutSeconds $Context.TimeoutSeconds
    Save-UiTestScreenshot -Context $Context -Target $fixture -Name 'ocr-windows-ai-fixture' | Out-Null
}
