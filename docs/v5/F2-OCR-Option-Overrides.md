# F2 — Per-call OCR option overrides

| | |
|---|---|
| Track | Foundation |
| Size / risk | Small / Low |
| Recommended model | Sonnet |
| Depends on | — |
| Unblocks | F3 |
| Branch | `v5/f2-ocr-options` |

Read [README.md](README.md) first.

## Goal

Let a caller OCR with options that differ from the user's saved settings, without changing those settings. A workflow node needs this ("OCR with error-correction off"); today the only way is to mutate global settings.

## Current state (verify)

- Headless OCR code reads `SettingsAccess.Current` (an `ITextGrabSettings`) directly. Find every read: search `SettingsAccess.Current` in `Text-Grab.Core` and `Text-Grab.Core.Windows` (26 hits on 2026-11-09).
- The OCR-relevant ones are in `Text-Grab.Core.Windows/Models/OcrOutput.cs` (`CleanOutput`: `CorrectToLatin`, `CorrectErrors`) and `Text-Grab.Core.Windows/Utilities/OcrUtilities.cs` (`BuildTextFromOcrLines` and helpers: `RemoveFurigana`, `ParagraphDetection`), plus `TryToReadBarcodes` and `UseTesseract` in the dispatch path.
- Correction (verified when F2 was built): `TryToReadBarcodes` is read only in the app (`Text-Grab/Utilities/OcrSourceUtilities.cs`, `GrabFrame`), and `UseTesseract` only in `CaptureLanguageUtilities` (language list, not text shaping). Neither is in Core.Windows text shaping, so `OcrOptions` carries `TryToReadBarcodes` but nothing in Core.Windows consumes it yet; the app dispatch path adopts it in F3.
- Quirk: on the space-joining path `GetTextFromOcrLine` discards the return value of `StringBuilder.TryFixEveryWordLetterNumberErrors`, so `CorrectErrors` has no effect there. F2 preserved it (pinned in `OcrSettingsCharacterizationTests`).
- `Tests.Core.Windows/FakeTextGrabSettings.cs` supplies settings to headless tests.

## Design

In `Text-Grab.Core` (namespace `Text_Grab.Models`):

```csharp
public sealed record OcrOptions(
    bool CorrectErrors,
    bool CorrectToLatin,
    bool RemoveFurigana,
    bool ParagraphDetection,
    bool TryToReadBarcodes,
    string LanguageTag)
{
    public static OcrOptions FromSettings(ITextGrabSettings settings, string? languageTag = null);
}
```

Each headless method that reads one of these settings gains an optional trailing `OcrOptions? options = null` parameter. `null` means "use `OcrOptions.FromSettings(SettingsAccess.Current)`", which is exactly today's behavior. Read the settings once at the top of the method and pass the record down; do not re-read `SettingsAccess.Current` deeper in the call chain when an options object is in hand.

## Steps

1. List every OCR-related `SettingsAccess.Current` read and the public method it sits under. Put the list in your report.
2. Add `OcrOptions` with unit tests in `Tests.Core` for `FromSettings`.
3. Add characterization tests in `Tests.Core.Windows` for `CleanOutput` and `BuildTextFromOcrLines` across option combinations, driven through `FakeTextGrabSettings`. They must pass before step 4.
4. Thread the optional parameter through. Private helpers take a non-null `OcrOptions`.
5. Add tests that pass explicit options differing from the fake settings and assert the options win.

## Out of scope

- Moving any OCR code between projects (F3).
- TTS, HDR, audio or other non-OCR settings.
- Changing `ITextGrabSettings`.

## Verification

All three test executables pass. No existing call site was edited (the parameter is optional). The existing OCR image tests (`Tests.Core.Windows/OcrTests.cs`) pass unchanged.

## Done when

Every OCR text-shaping decision in Core.Windows can be overridden per call, and the default path is byte-for-byte the old behavior.

## Traps

- `OcrTests` depends on `RemoveFurigana = true` in the fake settings. Do not change the fake's defaults.
- Optional parameters on `public` methods are a binary-compatible-looking but source-level change; any overload that already has a trailing optional parameter can become ambiguous. Build the whole solution, not just the library.
