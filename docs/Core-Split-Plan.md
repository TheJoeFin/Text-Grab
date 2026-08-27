# Core Split Plan

Reorganizing Text-Grab from one 178-file WPF app into a layered set of projects:

```
Text-Grab.Core           net10.0                        pure logic, no UI, no Windows
   ^
Text-Grab.Core.Windows   net10.0-windows10.0.22621.0    WinRT / GDI+ / P-Invoke, UseWPF=false
   ^
Text-Grab                net10.0-windows...  WPF app     Views, Controls, Pages, app wiring
```

Test projects mirror the same tiers: `Tests.Core` (net10.0, fast), `Tests.Core.Windows`
(to be created), `Tests` (net10.0-windows, WPF/STA, references the app).

Phase 0 (scaffolding) and the first six move commits are already done — see
`git log --oneline` from `288f6d1` forward. This document is the plan for the rest and
the standing contract for every agent that works on it.

---

## 1. Invariants — every agent must follow these

1. **All three projects share `RootNamespace = Text_Grab`.** A moved file keeps its
   `namespace` line unchanged. A move is `git mv` plus fixing only what actually breaks.
   Never "tidy" namespaces during a move.
2. **`Text-Grab.Core.Windows` keeps `UseWPF=false` and `UseWindowsForms=false`.** If a file
   needs `System.Windows.*` or `System.Windows.Forms.*`, it either stays in the app or gets
   split. Do not flip these flags to make a move work.
3. **Dependencies point one way only:** app → Core.Windows → Core. Core never references
   Core.Windows; neither library ever references the app.
4. **One batch = one commit.** Do not start the next batch until the current one builds.
5. **Defer, don't redesign.** If a file in your list turns out to be blocked by something
   outside your list, leave it, record it in §7 (Deferred ledger) with the specific blocker,
   and move on. Do not expand scope to unblock it.
6. **Never run two edit-capable agents against this working tree at once.** Moves touch
   shared files (`.csproj`, call sites, `Enums.cs`); concurrent edits corrupt each other.
   Reconnaissance agents (read-only) may run in parallel; movers run serially.
7. Commit messages follow the established style: what moved, what had to be fixed and why,
   what was deferred and its specific blocker. See `edefeaa` and `e677b54` for the pattern.

## 2. Verification gates — exact commands

```bash
# Primary gate. Builds Core -> Core.Windows -> app -> Tests. ~13s incremental.
dotnet build Tests/Tests.csproj -c Debug -p:Platform=x64

# Fast library gate, run first while iterating.
dotnet build Tests.Core/Tests.Core.csproj -c Debug

# Wave boundaries. Run the two fast ones first - they need no display and finish in ~1s each.
dotnet test --project Tests.Core/Tests.Core.csproj
dotnet test --project Tests.Core.Windows/Tests.Core.Windows.csproj -p:Platform=x64
dotnet test --project Tests/Tests.csproj -r win-x64   # slow, spawns STA/WPF tests
```

`Tests.Core.Windows` needs `-p:Platform=x64` (or another named platform). Without it the
Windows App SDK targets, pulled in transitively via `Text-Grab.Core.Windows`, fail with
`WindowsAppSDKSelfContained requires a supported Windows architecture`.

**Do not run `dotnet build Text-Grab.sln`.** The MSIX `Text-Grab-Package.wapproj` fails
under the dotnet CLI with `MSB4019: Microsoft.DesktopBridge.props was not found` — it needs
full MSBuild / Visual Studio. That error is pre-existing and unrelated to this work; the
per-project builds above are the real gate.

Also note: `Text-Grab.Core` and `Text-Grab.Core.Windows` must keep
`<RuntimeIdentifiers>win-x86;win-x64;win-arm64</RuntimeIdentifiers>`. The wapproj restores
project references per-RID and drops `NETSDK1047` without them.

## 3. Wave 0 — the three foundation decisions

These gate almost everything else and are design work, not mechanical moves. **Do these
yourself (Opus), serially, before dispatching any Sonnet mover.**

### B1 — Settings access

48 app files reach settings through `AppUtilities.TextGrabSettings`, which returns
`Text_Grab.Properties.Settings` — an `internal sealed partial class : ApplicationSettingsBase`
with 104 generated properties. Only **28 distinct properties** are actually read through that
accessor, so the real coupling surface is small.

**Done in `8398af2`:** `Text-Grab.Core/Interfaces/ITextGrabSettings.cs` declares the slice
portable code may read; `Text-Grab.Core/Services/SettingsAccess.cs` resolves it. Core code reads
`SettingsAccess.Current.CorrectToLatin` instead of `AppUtilities.TextGrabSettings.CorrectToLatin`.

The app implements the interface by *declaring* it — the generated properties already match on
name and type, and `Save()` comes from `ApplicationSettingsBase`, so there is no forwarding code:

```csharp
// Text-Grab/Properties/Settings.cs — existing hand-written partial
[SettingsProvider(typeof(AutomationSettingsProvider))]
internal sealed partial class Settings : ITextGrabSettings { }
```

`SettingsAccess` holds a `Func<ITextGrabSettings>` rather than an instance, because the app's
settings object hangs off `Singleton<SettingsService>.Instance`, which is lazy and does real work
on first touch. The app registers it from a `[ModuleInitializer]`, not from `App.appStartup`: the
`Tests` host loads the app assembly and runs its code without ever raising the WPF Startup event.

**Seeded properties** (from the actual near-term consumers, not guessed): `CorrectErrors`,
`CorrectToLatin`, `ParagraphDetection`, `RemoveFurigana`, `TryToReadBarcodes`,
`UiAutomationFallbackToOcr`, `UseTesseract`, `TesseractPath`, `LastUsedLang`, `Save()`.

**Adding a property:** add it to the interface. If the build then fails, the missing property
belongs in `Settings.settings` — do not write a forwarding property in the partial. If one move
would need more than a handful of new members, use the façade split instead.

**Prefer the cheaper alternative for leaf cases:** when a file has exactly one settings
touchpoint, split the pure part into Core and leave a thin settings-reading façade in the app.
That is what `e677b54` did with `PatternItem` / `PatternItemCatalog`, and it stays the right
move for one-off cases. Use the interface when a file has many touchpoints or the façade
would be larger than the thing it wraps.

### B2 — Geometry currency

`System.Windows.Rect` / `Point` / `Size` live in `WindowsBase.dll`, which only comes with
`UseWPF=true`. They appear in ~30 otherwise-portable files and are the single most common
blocker after settings.

**Done in `30af90f`:** `System.Drawing.RectangleF` / `PointF` / `SizeF` are the geometry type in
Core and Core.Windows. These are in **System.Drawing.Primitives**, part of the shared framework —
genuinely cross-platform, needs no Windows TFM and no package reference.
`Text-Grab.Core/Utilities/RectangleFExtensions.cs` carries the portable helpers (`IsGood`,
`CenterPoint`, `GetScaledUpByFraction`, `GetScaleSizeByFraction`, `Union`); the app's existing
`Extensions/ShapeExtensions.cs` gained the boundary conversions (`AsRect` / `AsRectangleF`,
`AsPoint` / `AsPointF`, `AsSize` / `AsSizeF`) alongside the `Rectangle` ↔ `Rect` pair it already
had. View code converts at the edge.

> Important distinction agents keep getting wrong: `System.Drawing.Primitives` (Rectangle,
> RectangleF, Point, PointF, Size, SizeF, Color) is portable and fine in **Core**.
> `System.Drawing.Common` (Bitmap, Graphics, Icon, BitmapData) is Windows-only and belongs in
> **Core.Windows**. A `using System.Drawing;` alone tells you nothing — check which types.

### B3 — Imaging currency

Movable code juggles four bitmap representations. The rule:

| Type | Assembly | Allowed in |
|---|---|---|
| `System.Drawing.Bitmap` | System.Drawing.Common | Core.Windows, app |
| `Windows.Graphics.Imaging.SoftwareBitmap` | WinRT | Core.Windows, app |
| `ImageMagick.MagickImage` | Magick.NET | Core.Windows, app |
| `System.Windows.Media.Imaging.BitmapSource` | WPF | **app only** |
| `byte[]` / `Stream` | — | Core |

`BitmapSource` never crosses out of the app. Core (pure) traffics in `byte[]`/`Stream`.

### Wave 0 batches — **done**

| Batch | Work | Commit |
|---|---|---|
| 0a | B2 geometry currency | `30af90f` |
| 0b | B1 settings seam | `8398af2` |
| 0c | Test scaffolding, tier guards, CI | this commit |

Three things came out differently than planned above; the text has been corrected to match
what was actually built:

- **0a extended `ShapeExtensions` instead of adding `WpfGeometryExtensions`.** That file already
  carried `Rectangle` ↔ `Rect`, so the conversions belonged next to it rather than in a parallel
  API. Core got `RectangleFExtensions` mirroring the portable helpers.
- **0b registers the resolver from a `[ModuleInitializer]`, not `App.appStartup`.** The `Tests`
  host loads the app assembly and exercises its code without ever raising the WPF Startup event,
  so wiring it at startup would have left every app-referencing test unable to read settings.
- **0c added `Tests.Core.Windows/TierBoundaryTests.cs`**, which was not in the original plan.
  It asserts by reflection that Core references no WPF/WinRT assembly, that Core.Windows
  references no WPF assembly, and that Core does not reference Core.Windows. This is the
  automated enforcement of invariants 2 and 3 — cheaper than catching a `UseWPF` flip in review.

## 4. Wave plan

Each batch is one commit, gated on §2. File lists are starting points — an agent that finds a
listed file blocked defers it per invariant 5.

### Wave 1 — pure leaves → Text-Grab.Core

**1a — Models (14 files).** `AsyncOcrFileResult`, `EditTextTableDocument`, `ExtractedPattern`,
`FindResult`, `GrabFrameTableEditState`, `GrabFrameWordGroupingMode`, `GrabTemplate`,
`LookupItem`, `NullAsyncResult`, `OcrDirectoryOptions`, `SpreadsheetUndoHistory`,
`TemplatePatternMatch`, `TemplateRecognizerMatch`, `ThirdPartyPackageInfo`.
All verified dependency-free. Expect a near-pure `git mv` batch.

**1b — Utilities, Extensions, Interfaces (14 files).** `Utilities/PatternExecutor`,
`ColumnSplitUtilities`, `NumericUtilities`, `LanguageHeuristics`, `ProtocolUtilities`,
`Singleton`, `StreamWrapper`, `Json`, `ThirdPartyNoticeUtilities`;
`Extensions/NumberExtensions`, `StringBuilderExtensions`; `Interfaces/ITtsEngine`;
`UndoRedoOperations/UndoRedo`, `ChangeWord`, `ResizeWordBorder`.
Note `Json.cs` is in namespace `Text_Grab.Helpers`, not `Text_Grab.Utilities` — keep it.

**1c — Enums consolidation.** `Text-Grab/Enums.cs` (132 lines) vs `Text-Grab.Core/Enums.cs`
(15 lines). Move every enum with no UI/Windows dependency into Core's file; leave WPF-typed
ones behind. Verify no duplicate definitions across the two assemblies.

### Wave 2 — calculation and text engine → Text-Grab.Core

**2a — CalculationService (3 files, ~2000 lines).** `CalculationService.cs`,
`.UnitMath.cs`, `.DateTimeMath.cs` are fully pure (NCalc + UnitsNet only). Move the
`NCalcAsync` and `UnitsNet` `PackageReference` entries into `Text-Grab.Core.csproj`; they can
stay in the app csproj too if app code still uses them directly — check before deleting.
High-value batch: unlocks `CalculatorTests` and `UnitConversionTests` for `Tests.Core`.

**2b — Markdown split.** `MarkdownDocumentUtilities.cs` (1168 lines) mixes Markdig AST
parsing (portable) with `FlowDocument`/`System.Windows.Documents` rendering (WPF). Split into
`Text-Grab.Core/Utilities/MarkdownParsing.cs` and keep the FlowDocument half in the app.
Move the `Markdig` package reference to Core.

### Wave 3 — Windows leaves → Text-Grab.Core.Windows

**3a — Registry and interop.** `Utilities/ContextMenuUtilities`, `FileAssociationUtilities`,
`RegistryMonitor` (all `Microsoft.Win32.Registry`); `NativeMethods.cs`; `OSInterop.cs`
(1292 lines, `System.Windows.Forms` reference — verify whether it is a real WinForms
dependency or just a `using`; if real, this file stays in the app).

**3b — WinRT storage and language leaves.** `Extensions/StorageFileExtensions`,
`SettingsStorageExtensions`, `SoftwareBitmapExtensions`; `Models/GeneratedOcrLinesWords`,
`UiAutomationLang`, `WindowsAiLang`, `WindowsAiDescriptionLang`;
`Extensions/LanguageExtensions` (tagged WPF — check whether that is load-bearing).

**3c — Windows AI.** `Utilities/LimitedAccessFeatureUtilities`, `WinAiLanguageModel` (606),
`WinAiTranslator` (498), `WinAiMeetingNotes`, `ImplementAppOptions`,
`DesktopNotificationManagerCompat` (512). `WindowsAiUtilities` (408) is settings + GDI+
coupled — attempt after B1 lands, defer if it drags in more.

### Wave 4 — the OCR pipeline (the pivotal cut)

**4a — `OcrUtilities.cs` split (1061 lines). Opus owns this one.** It is the hinge of the
whole reorganization: headless OCR orchestration tangled with WPF adapters, plus a hidden WPF
dependency in `LoadBitmapFromFile`, plus `private static readonly Settings DefaultSettings =
AppUtilities.TextGrabSettings` at class scope. Target shape:
- `Text-Grab.Core.Windows/Ocr/OcrEngine.cs` — engine selection, language handling,
  WinRT/Windows-AI/Tesseract dispatch, result assembly.
- `Text-Grab/Utilities/OcrUiAdapters.cs` — everything returning or consuming `BitmapSource`,
  `System.Windows.Rect`, `Text_Grab.Controls.*`.
Do this as its own commit with nothing else in it.

**4b — OCR periphery.** `Models/OcrOutput` (blocks `BarcodeUtilities` — it reads settings
directly inside `CleanOutput()`), `Utilities/BarcodeUtilities`, `PdfDocumentRenderer` (497),
the remainder of `TesseractHelper` (401 — `GetTesseractPath` has a settings-write side effect
that must be lifted out first).

**4c — Language services and tables.** `Services/LanguageService` (392),
`Utilities/CaptureLanguageUtilities`, `LanguageUtilities`, and `Models/ResultTable` (950 —
needs B2 for `System.Windows.Rect` and decoupling from `Windows.Media.Ocr.OcrResult`; consider
having it consume the existing `IOcrLinesWords` from
`Text-Grab.Core.Windows/Models/OcrLinesWords.cs` instead).

### Wave 5 — capture and imaging → Text-Grab.Core.Windows

**5a.** `Utilities/ImageMethods` (WPF + WinRT + GDI+ + settings — expect a split, not a move),
`ImageChangeDetector`, `MagickHelpers`, `FileUtilities` (400).

**5b.** `Utilities/Hdr/HdrToneMapper` (already pure — could go to plain Core),
`Hdr/DisplayHdrInfo`, `Hdr/HdrScreenCapture` (504), `FreeformCaptureUtilities`,
`CameraCaptureUtilities`, `ClipboardUtilities` (464, WinForms clipboard — likely stays),
`Models/DragDataObject`.

### Wave 6 — services and settings

**6a.** `Services/SettingsService` (836), `Utilities/AutomationProfile`,
`AutomationSettingsProvider`, `SettingsImportExportUtilities` (435). `AutomationSettingsProvider`
derives from `LocalFileSettingsProvider` (System.Configuration) — that package works on
net10.0, so plain Core is plausible; verify before assuming.

**6b.** `Services/HistoryService` (1004), `Utilities/GrabTemplateManager`,
`GrabTemplateExecutor` (628), `PostGrabActionManager`, `CustomBottomBarUtilities`,
`ShortcutKeysUtilities`.

**6c.** `Services/TtsService`, `WindowsSpeechEngine`, `Utilities/AudioTranscriptionUtilities`
(1115), `UIAutomationUtilities` (1362), `DiagnosticsUtilities` (670).

### Wave 7 — tests and closeout

**7a — test migration.** Move to `Tests.Core`: `StringMethodTests`,
`TextSearchUtilitiesTests`, `RecognizerExecutorTests`, `PatternExecutorTests`,
`CalculatorTests`, `UnitConversionTests`, `ExtractedPatternTests`,
`ColumnSplitUtilitiesTests`, `SpreadsheetUndoHistoryTests`, `EditTextTableDocumentTests`,
`GrabFrameTableEditStateTests`, `ThirdPartyNoticeUtilitiesTests`, `ProtocolUtilitiesTests`,
plus whatever else became pure. Move Windows-but-headless tests into `Tests.Core.Windows`.
Delete `Tests.Core/ScaffoldingSmokeTests.cs` once real tests exist.

**7b — closeout.** Remove dead app-side shims; update `.github/workflows/*.yml` to run all
three test projects; confirm the MSIX package still builds in Visual Studio (the one thing the
CLI gate cannot check); update `BUILT-WITH.md` / this document with the final layer map.

### Never moves

`Views/`, `Controls/`, `Pages/`, `Styles/`, `Themes/`, `App.xaml.cs`, `AssemblyInfo.cs`,
`WPFExtensionMethods.cs`, `Properties/Settings.Designer.cs`, `Extensions/ControlExtensions`,
`DapploExtensions`, `KeyboardExtensions`, `ShapeExtensions`, `Utilities/ColorHelper`,
`CursorClipper`, `GrabFrameViewScaleUtilities`, `NotificationUtilities`,
`WindowSelectionUtilities`, `WindowResizer`, `WindowUtilities`, `HotKeyManager`,
`AutomationDiagnostics`, `NotifyIconUtilities`, `OutputUtilities`, `ShareTargetUtilities`,
and the WPF-typed `UndoRedoOperations` (`AddWordBorder`, `RemoveWordBorder`, `ChangedImage`,
`Operation`) and Models (`FullscreenCaptureResult`, `PostGrabContext`, `ShortcutKeySet`,
`TemplateRegion`, `UiAutomationOptions`, `UiAutomationOverlayItem`,
`UiAutomationOverlaySnapshot`, `WindowSelectionCandidate`, `WordBorderInfo`, `ButtonInfo` —
the last uses `Wpf.Ui.Controls`).

Several of the Models above become movable once B2 lands. Revisit them in Wave 7 rather than
guessing early.

## 5. Sub-agent orchestration

### Roles

| Role | Model | Isolation | Parallel? |
|---|---|---|---|
| **Cartographer** — read-only dependency mapping of one area | Sonnet, `Explore` | none (read-only) | **yes**, 4–6 at once |
| **Mover** — executes one batch, commits it | Sonnet, `general-purpose` | none (main tree) | **no**, strictly serial |
| **Architect** — Wave 0, batch 4a, any split that changes a public shape | Opus (you) | none | n/a |
| **Verifier** — build + test at wave boundaries | Sonnet | none | no |

### Why movers are serial

Every batch touches shared state: `.csproj` `PackageReference` lists, `Enums.cs`, and call
sites in `EditTextWindow.xaml.cs` (7799 lines) and `GrabFrame.xaml.cs` (6442 lines) that
almost every batch edits. Worktree isolation would just relocate the conflict to a merge that
is harder to resolve than the original edit. Serial movers with a 13-second build gate between
them is the faster path in wall-clock terms.

The parallelism worth having is in reconnaissance: dispatch cartographers for Waves 3–6
simultaneously while you do Wave 0, so every mover starts with an accurate file list.

### Mover prompt template

```
You are executing batch <ID> of the Text-Grab Core split.

Read D:\source\TheJoeFin\Text-Grab\docs\Core-Split-Plan.md first — sections 1, 2, and
your batch in section 4 are binding. Then read the two most recent move commits
(git show edefeaa, git show e677b54) to match the established style.

Your file list is exactly:
  <paths>
Target project: <Text-Grab.Core | Text-Grab.Core.Windows>

Procedure, per file:
 1. Read it. Confirm its actual dependencies — do not trust `using` lines alone.
    `System.Drawing` primitives (RectangleF/PointF/SizeF) are portable and fine in Core;
    `System.Drawing` GDI+ (Bitmap/Graphics/Icon) is Core.Windows only.
 2. If it moves cleanly: `git mv` it, keep the namespace, fix call sites the compiler flags.
 3. If it needs a split: pure part moves, the coupled façade stays in the app under a new
    name. See PatternItem/PatternItemCatalog in e677b54 for the shape.
 4. If it is blocked by something outside your list: LEAVE IT. Do not expand scope.

After each file, run:  dotnet build Tests/Tests.csproj -c Debug -p:Platform=x64
Never run `dotnet build Text-Grab.sln` — the wapproj fails under the dotnet CLI by design.

When the whole list is done and the build is clean:
 - Append every deferred file to section 7 of Core-Split-Plan.md with its specific blocker.
 - Commit everything as one commit in the established style.

Report back: files moved, files split (and how), files deferred (and why), final build status.
Do not report success unless the build actually succeeded — paste the failure if it did not.
```

### Cartographer prompt template

```
Read-only reconnaissance for the Text-Grab Core split. Make no edits.

Area: <e.g. "the capture and imaging code — Utilities/ImageMethods.cs, Utilities/Hdr/*,
FreeformCaptureUtilities, CameraCaptureUtilities, ClipboardUtilities, MagickHelpers">

For each file report:
 - Its real dependency set: WPF types, WinForms types, WinRT namespaces, System.Drawing
   primitives vs GDI+, P/Invoke, settings access, and which other Text-Grab types it needs.
 - Verdict: moves clean to Core / moves clean to Core.Windows / needs a split (say where the
   seam is) / stays in the app (say why).
 - Which OTHER files a move would drag in.
Rank the area into tiers: move-now, move-after-B1-settings, move-after-B2-geometry, never.
Be specific about blockers — "uses settings" is useless; "GetTesseractPath writes
TesseractPath back to settings as a side effect" is what I need.
```

### Cadence

Do not fire-and-forget the whole chain. Run **one wave at a time**, and between waves:
`git log --oneline -8`, run the wave-boundary test commands, and skim the batch diffs. The
prior six commits were all human-reviewed; that ratio should hold — a mover that silently
"fixes" a call site incorrectly compiles fine and breaks at runtime.

## 6. Risk register

| Risk | Mitigation |
|---|---|
| MSIX packaging breaks (CLI gate can't see it) | Open the solution in VS and build the wapproj at each wave boundary. |
| A mover flips `UseWPF=true` on Core.Windows to unblock itself | `Tests.Core.Windows/TierBoundaryTests.cs` fails the build. Also check the csproj diff in review. |
| Silent behavior change from a "cleanup" during a move | Movers are told to change only what the compiler flags. Review diffs for unrequested edits. |
| `RuntimeIdentifiers` dropped from a library csproj | Causes NETSDK1047 in the wapproj restore only — invisible to the CLI gate. Grep the csprojs at wave boundaries. |
| Settings interface sprawls to all 104 properties | Add properties only when a move demands one; if a batch needs more than ~5 new ones, that file probably wants a façade split instead. |
| `EditTextWindow.xaml.cs` / `GrabFrame.xaml.cs` churn | They are touched by most batches. Serial movers make this safe; parallel ones would not. |

## 7. Deferred ledger

Files attempted and left behind, with the specific blocker. **Movers append here.**

| File | Blocker | Unblocked by |
|---|---|---|
| `Utilities/OcrUtilities.cs` | 1061 lines mixing headless orchestration with WPF adapters; hidden WPF dep in `LoadBitmapFromFile`; class-level `AppUtilities.TextGrabSettings` field | Batch 4a (Opus) |
| `Utilities/TesseractHelper.cs` (class) | `GetTesseractPath()` writes back to settings as a side effect | B1 + lifting the write out |
| `Services/LanguageService.cs` | settings + WPF coupled; drags in several untouched types | B1, batch 4c |
| `Utilities/WindowsAiUtilities.cs` | settings + WPF coupled; drags in several untouched types | B1, batch 3c |
| `Utilities/BarcodeUtilities.cs` | `OcrOutput.CleanOutput()` reads settings directly | B1, batch 4b |
| HDR / WGC capture (`Utilities/Hdr/*`) | separate area, mostly clean already | batch 5b |

## 8. Definition of done

- `Text-Grab.Core` holds the text, pattern, table, calculation, and template logic, with no
  `System.Windows`, no `Windows.*`, no P/Invoke.
- `Text-Grab.Core.Windows` holds OCR engines, capture, imaging, Windows AI, and Win32 interop,
  with `UseWPF=false` still set.
- `Text-Grab` holds Views, Controls, Pages, app wiring, and thin adapters — nothing else.
- `Tests.Core` runs in ~2s with no display and covers the pure tier; `Tests` keeps only the
  WPF/STA tests.
- CI runs all three test projects; the MSIX package builds in Visual Studio.
- Section 7 is empty, or every remaining row has a written reason it stays.
