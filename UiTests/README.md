# UI automation

`coverage.json` is the source-of-truth inventory for all 154 checks from the external manual checklist. Each automated check maps to an exact `Suite/test` registration and records `full`, `partial`, `contract`, or `capability-gated` coverage. The three subjective checks remain documented manual exceptions.

## Prerequisites and safety

- Windows 10/11, .NET SDK 10.0.100, and **WinApp CLI 0.5.x** (`winapp --version`).
- A logged-in, unlocked desktop is needed only for real-input/system lanes. Do not run them from a service session.
- The harness creates a per-run profile below `UiTests\artifacts`, snapshots/restores the clipboard, and terminates only PIDs it created. Do not point it at a normal Text Grab profile.
- Standard runs do not enable hotkeys, manipulate the tray, install packages, or alter user settings. Real input, Explorer, clipboard, and OLE drag/drop tests are opt-in system integration.
- The packaged lane is destructive and refuses to run unless it is explicitly selected **and** `TEXT_GRAB_DISPOSABLE_VM=1` is set on a resettable VM.

## Local commands

```powershell
pwsh .\UiTests\Validate-Coverage.ps1
pwsh .\UiTests\New-CoverageReport.ps1
pwsh .\UiTests\Test-ReleaseSignoffSemantics.ps1
pwsh .\UiTests\Run-UiTests.ps1 -Suite Harness,Lifecycle,CliProtocol,GrabFrame,EditText,QuickLookup,Settings,PatternsBulk
pwsh .\UiTests\Invoke-ReleaseSignoff.ps1
```

The release sign-off runs coverage validation, xUnit, and the safe standard suites. It writes `release-signoff.json` and `release-signoff.md` beneath `UiTests\artifacts\release-signoff`, with per-lane and aggregate UI pass/fail/skip counts, skipped test names/reasons, and paths to UI JSON/JUnit, environment, screenshots, recordings, and diagnostics. A lane needs at least one executed (pass or fail) UI test; the safe standard lane can therefore pass with documented boundary/capability skips after its smoke minimum executes. Explicitly requested capability, system-integration, and packaged-VM lanes fail sign-off when unavailable or all-skipped, including when the packaged VM safety confirmation is absent.

Opt in only on the appropriate runner:

```powershell
pwsh .\UiTests\Invoke-ReleaseSignoff.ps1 -IncludeCapabilities
pwsh .\UiTests\Invoke-ReleaseSignoff.ps1 -IncludeSystemIntegration
$env:TEXT_GRAB_DISPOSABLE_VM = '1'
pwsh .\UiTests\Invoke-ReleaseSignoff.ps1 -IncludePackagedVm
```

## Suites and artifacts

Safe suites are `Harness`, `Lifecycle`, `CliProtocol`, `GrabFrame`, `EditText`, `QuickLookup`, `Settings`, and `PatternsBulk`. Capability suites are `DisplayHardware`, `OcrCapability`, `NotificationsTtsShare`, and `OsArchitecture`; unsupported capability checks skip with their reason. `SystemIntegration` uses real hotkeys, tray, clipboard, focus, and OLE input. `PackagedVm` checks package contracts and delegates installation lifecycle to the disposable-VM script.

Every run writes `results.json`, `junit.xml`, `environment.json`, diagnostics, and failure screenshots (plus recordings when `-Record` is used) under a unique run directory. Start triage with `results.json`, then inspect its screenshot, `environment.json` capability values, and diagnostics. A skip is expected only when its stated capability is absent; a failure is release-blocking until explained.

## CI runners

`.github\workflows\interactive-ui.yml` is deliberately not a PR gate. Register a dedicated, logged-in self-hosted desktop with the matrix labels shown there (`text-grab-ui-x64`, `text-grab-ui-system`, `text-grab-ui-mixed-dpi`, `text-grab-ui-packaged`, `text-grab-ui-arm64`, or `text-grab-ui-copilot-plus`). Set `TEXT_GRAB_INTERACTIVE_CAPABILITIES` only for capabilities actually provisioned. Enable specialized `all-enabled` lanes through their documented repository variables. The packaged runner also needs `TEXT_GRAB_DISPOSABLE_VM=1`.

## Remaining manual-only checks

Only `app-tray-icon-art`, `tts-read-text`, and `tts-voice-selection` require a human judgment of artwork or audible output. All other inventory entries have an implementation mapping; partial and contract entries state the remaining assertion boundary in `coverage.json`.
