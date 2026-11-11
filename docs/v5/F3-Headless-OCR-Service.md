# F3 — Headless OCR service

| | |
|---|---|
| Track | Foundation |
| Size / risk | Medium / Medium |
| Recommended model | Opus |
| Depends on | F2 |
| Unblocks | W1 (OCR nodes), the folder-tree pane in E5 |
| Branch | `v5/f3-ocr-service` |

Read [README.md](README.md) first, then `../Core-Split-Plan.md` §4.4 (batch 4c) and the `OcrSourceUtilities` row in its §7.

## Goal

Make "OCR this file / stream / screen rectangle and give me text" callable from `Text-Grab.Core.Windows` with no WPF, behind an `IOcrService` interface declared in Core.

## Current state (verify)

`Text-Grab/Utilities/OcrSourceUtilities.cs` (646 lines, app-side) mixes three kinds of code:

| Kind | Members (names as of 2026-11-09) | Destination |
|---|---|---|
| Engine dispatch, no WPF | `GetTextFromImageAsync(Bitmap, ILanguage)`, `GetOcrResultFromImageAsync` (both overloads), `GetOcrResultFromBitmapAsync`, `GetOcrResultFromRegionAsync(Rectangle, …)`, `GetTextFromBitmapAsync`, `GetTextFromBitmapAsTableAsync`, `GetTextFromAbsoluteRectAsync`, `GetTextFromRandomAccessStream`, `GetTextFromWinAiAsync`, `GetTextFromWinAiDescriptionAsync`, `GetIdealScaleFactorForOcrAsync` | Core.Windows |
| File OCR, blocked by one method | `OcrAbsoluteFilePathAsync`, `OcrFile`, private `LoadBitmapFromFile` | Core.Windows after the rewrite below |
| WPF adapters and UI orchestration | `GetRegionsTextAsync(Window, …)`, `GetRegionsTextAsTableAsync(Window, …)`, `GetClickedWordAsync(Window, …)`, the `BitmapSource` overloads, `GetCopyTextFromPreviousRegion`, `GetTextFromPreviousFullscreenRegion(TextBox?)` | Stay in the app |

The single WPF dependency in the file-OCR path is `LoadBitmapFromFile` (~line 491): it builds a `BitmapImage` and calls `ImageMethods.RotateImage` to apply EXIF rotation, then converts to a GDI+ `Bitmap`. `BitmapUtilities.GetRotateFlipType(path)` — the part that reads the EXIF orientation — is already in Core.Windows.

`OcrFile` also calls `GrabTemplateExecutor.ExecuteTemplateOnBitmapAsync`, which is app-side. PDF input goes through `PdfDocumentRenderer`, also app-side (returns `BitmapSource`).

**Census, step 3 (2026-11-09, `v5/f3-6`). The table above is wrong in four places:**

1. **The dispatch group is not WPF-free.** `GetTextFromImageAsync`, `GetOcrResultFromBitmapAsync`, `GetOcrResultFromRegionAsync` and `GetTextFromBitmapAsTableAsync` all call `ImageMethods.ScaleBitmapUniform`, which scales through a WPF `BitmapImage` + `TransformedBitmap` + `ScaleTransform`. Every Windows-OCR (`GlobalLang`) call goes through it. Fix: a delegate-resolver seam, `Text-Grab.Core.Windows/Services/BitmapScalingAccess.cs`. The app registers the unchanged WPF scaler from a `[ModuleInitializer]`, so the app's scaled pixels do not change. A host with nothing registered gets a GDI+ bicubic default.
2. **`GetTextFromAbsoluteRectAsync` stays in the app.** It takes a WPF `Rect`, calls `UIAutomationUtilities` (app-only by Core-Split B4) and captures through `ImageMethods.GetRegionOfScreenAsBitmap`, which writes to `HistoryService` (Core-Split 5c).
3. **`GetOcrResultFromRegionAsync` stays in the app** for the same `GetRegionOfScreenAsBitmap` reason. Its body after the capture is identical to `GetOcrResultFromBitmapAsync`, so it becomes a two-line wrapper over that method.
4. **`OcrAbsoluteFilePathAsync` cannot move whole.** It has a PDF branch through `PdfDocumentRenderer`, as `OcrFile` does. The image path moves down as a new method, `OcrImageFileAsync`. `OcrAbsoluteFilePathAsync` stays in the app as the PDF-aware wrapper, so its 8 callers are unchanged.

Also: `LoadBitmapFromFile`'s rotation (via `RotateImage`) applies only the three pure rotations. EXIF 2/4/5/7 (mirrored) are never flipped, and 5/7 are not rotated either. The sketch `bitmap.RotateFlip(GetRotateFlipType(path))` in step 2 would have changed that, so the rewrite keeps the old switch.

Corrected destinations:

| Kind | Members | Destination |
|---|---|---|
| Engine dispatch | `GetTextFromImageAsync`, `GetOcrResultFromImageAsync` (both), `GetOcrResultFromBitmapAsync`, `GetTextFromBitmapAsync`, `GetTextFromBitmapAsTableAsync`, `GetTextFromRandomAccessStream`, `GetTextFromWinAiAsync`, `GetTextFromWinAiDescriptionAsync`, `GetIdealScaleFactorForOcrAsync`, private `GetTextFromOcrResult` / `GetWindowsAiDescriptionOcrResultAsync` / `GetIdealScaleFactorForOcrResult`; `IsUiAutomationLanguage` / `GetCompatibleOcrLanguage` become `internal` (the app half still calls them) | Core.Windows |
| File OCR, image path | `LoadBitmapFromFile` (internal), new `OcrImageFileAsync` | Core.Windows |
| App-bound | `GetTextFromAbsoluteRectAsync`, `GetOcrResultFromRegionAsync` (wrapper), `OcrAbsoluteFilePathAsync` (PDF wrapper), `OcrFile` (PDF + template wrapper), `GetRegionsTextAsync` and `GetRegionsTextAsTableAsync` (no callers; left alone), the `BitmapSource` overloads, `GetClickedWordAsync` and its two private helpers, `GetCopyTextFromPreviousRegion`, `GetTextFromPreviousFullscreenRegion`, `CanReplayPreviousFullscreenSelection`, `GetExcludedWindowHandles` | Stay in the app |

Name: 21 external references point at members that stay and 16 at members that move. Following batch 4c, the app half keeps `OcrSourceUtilities` and the moved half takes a new name, `OcrEngineUtilities`.

## Design

Interface in `Text-Grab.Core` (`Text_Grab.Core.Abstractions`):

```csharp
public interface IOcrService
{
    Task<string> OcrFileAsync(string path, OcrOptions? options = null, CancellationToken ct = default);
    Task<string> OcrImageAsync(Stream image, OcrOptions? options = null, CancellationToken ct = default);
    Task<string> OcrScreenRegionAsync(RectangleF screenRegion, OcrOptions? options = null, CancellationToken ct = default);
}
```

Implementation `OcrService` in `Text-Grab.Core.Windows`, a thin class over the moved static methods. Language is resolved from `OcrOptions.LanguageTag` through the existing `LanguageService` / `LanguageUtilities`. Register it in `AppServices` if F1 has landed; if it has not, leave registration as a noted follow-up (F3 does not depend on F1).

Keep the class name `OcrSourceUtilities` for the half that moves down if the call-site census favors it, as batch 4c did for `OcrUtilities`. Count references first, then decide which half keeps the name.

## Steps

1. **EXIF fixtures first.** Add test images for EXIF orientations 1, 3, 6 and 8 (a small asymmetric image re-saved with each tag) under the test images, with a test that OCRs or pixel-checks each through the *current* `LoadBitmapFromFile` path. These must pass before the rewrite.
2. **Rewrite `LoadBitmapFromFile` without WPF.** Read the file into memory, create the `Bitmap` from a `MemoryStream` (so the file is not locked), and apply `bitmap.RotateFlip(BitmapUtilities.GetRotateFlipType(path))`. Step 1's tests must still pass.
3. **Census.** List every member of `OcrSourceUtilities`, the types it touches, and its callers. Confirm the table above; correct it in this file if it is wrong.
4. **Move the dispatch and file-OCR members** to Core.Windows. Thread `OcrOptions?` through (from F2). Fix only what the compiler flags.
5. **Deal with `OcrFile`'s template call.** If `GrabTemplateExecutor.ExecuteTemplateOnBitmapAsync` cannot move, split `OcrFile`: the plain path moves down; the template branch stays in an app-side wrapper that calls the moved method. Do not move `GrabTemplateExecutor` in this phase.
6. **Add `IOcrService` and `OcrService`**, with tests in `Tests.Core.Windows` that OCR an existing fixture image by path and by stream.
7. Update `../Core-Split-Plan.md` §7: rewrite or remove the `OcrSourceUtilities` row.

## Out of scope

- PDF input to `IOcrService` (needs `PdfDocumentRenderer`, blocked on `BitmapSource`). `OcrFileAsync` on a PDF may throw `NotSupportedException` for now; say so in the XML doc.
- Moving `GrabTemplateExecutor` / `GrabTemplateManager`.
- Any change to capture UI (`FullscreenGrab`, `GrabFrame`).

## Verification

- All three test executables pass, including the full `OcrTests` suite and the new EXIF tests.
- `TierBoundaryTests` passes.
- Manual: fullscreen grab, Grab Frame grab, a Tesseract language, an HDR monitor if available, a barcode image, a rotated phone photo dropped on the Edit Text Window, "Read folder of images" on a folder with mixed orientations.

## Done when

A test in `Tests.Core.Windows` (no WPF, no app assembly) can OCR an image file through `IOcrService` and get the same text the app produces.

## Traps

- `new Bitmap(path)` keeps the file locked until disposal and `Image.FromFile` does the same. Load through a stream copy.
- `GetOcrResultFromRegionAsync` captures the screen; check what it calls for capture (`BitmapUtilities.CaptureScreenRegion` is `internal` in Core.Windows) before assuming it moves cleanly.
- `System.Drawing.Rectangle` is fine in Core.Windows; `System.Windows.Rect` and `System.Windows.Point` are not. `GetClickedWordAsync` uses the WPF `Point`.
- Several methods here are `async void` UI entry points. They stay in the app.
