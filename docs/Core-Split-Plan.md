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
(net10.0-windows, headless), `Tests` (net10.0-windows, WPF/STA, references the app).

Phase 0 (scaffolding), the first six move commits, and Wave 0 (foundations) are done — see
`git log --oneline` from `288f6d1` forward. This document is the plan for the rest and
the standing contract for every agent that works on it.

**Section 4's file lists were rebuilt from five parallel reconnaissance passes** that read every
candidate file end-to-end. They supersede the original lists, which were derived from grepping
`using` directives and were wrong in roughly a dozen places (§4.0).

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
8. **Never classify a file by its `using` directives.** Check which *types* it actually uses.
   The original wave lists in this document were built by grepping usings and were wrong about
   a dozen files in both directions — see §4.0. The four traps, all of which bit that pass:
   - A file with no `System.Windows` using can still be WPF-bound: `using Text_Grab.Controls;`
     reaches `WordBorder`, which *is* a WPF `Control`.
   - A fully-qualified type never appears in a using at all
     (`Wpf.Ui.Controls.SymbolRegular` in `LookupItem`).
   - `System.Drawing` is two different things. Primitives (`RectangleF`, `PointF`, `SizeF`,
     `Color`) are portable and fine in **Core**; GDI+ (`Bitmap`, `Graphics`, `Icon`) is
     Windows-only and belongs in **Core.Windows**.
   - `Rect` is two different things. `Windows.Foundation.Rect` is WinRT and fine in
     Core.Windows; `System.Windows.Rect` is WindowsBase and is not. `edefeaa` already hit
     this once.

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

Note that `Magick.NET` splits the same way its consumers do: `Magick.NET.SystemDrawing` is a
plain net8.0 library and is Core.Windows-eligible, while `Magick.NET.SystemWindowsMedia` is
WPF-only and must stay in the app.

### B4 — UI Automation: the `FrameworkReference` loophole is closed

`System.Windows.Automation` (`UIAutomationClient.dll`) lives in the WindowsDesktop shared
framework. It can be resolved with `<FrameworkReference Include="Microsoft.WindowsDesktop.App" />`
*without* setting `UseWPF=true`, which is a real gap in the wording of invariant 2.

**Decision: do not take it.** That reference also drags in `WindowsBase`, which puts
`System.Windows.Rect` back within reach of Core.Windows and quietly defeats B2. The one file this
affects, `UIAutomationUtilities.cs`, stays in the app — its type surface is four never-move models
deep, which is not worth weakening the tier boundary to move one file.

`Tests.Core.Windows/TierBoundaryTests.cs` already enforces this: it checks referenced assembly
*names*, and `WindowsBase` is on its list, so the loophole fails the build rather than passing
review.

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

### 4.0 What reconnaissance changed

Five read-only passes read every candidate file end-to-end. The corrections that matter:

**Moved *out* of the wave lists (now never-move):**

| File | Why the original list was wrong |
|---|---|
| `UndoRedoOperations/UndoRedo.cs`, `ChangeWord.cs`, `ResizeWordBorder.cs` | Hold `WordBorder` fields and construct WPF operation classes. No `System.Windows` using — they reach WPF through `using Text_Grab.Controls;`. |
| `Models/LookupItem.cs` | `Wpf.Ui.Controls.SymbolRegular UiSymbol`, fully qualified, so no using revealed it. |
| `Utilities/ImplementAppOptions.cs` | Filed under Windows AI; is actually app-lifecycle plumbing that casts to the WPF `App` and calls the never-move `NotifyIconUtilities`. |
| `Utilities/MagickHelpers.cs` | Every public signature is `ImageSource`→`ImageSource`; its `Magick.NET.SystemWindowsMedia` package is WPF-only. |
| `Utilities/CameraCaptureUtilities.cs` | Both failure paths show `Wpf.Ui.Controls.MessageBox`; entry point needs a WPF `Window` for its `hwnd`. |
| `Utilities/SettingsImportExportUtilities.cs` | Orchestration glue over three other services; reflects over the *entire* settings surface by design. |
| `Utilities/DiagnosticsUtilities.cs` | Reads ~70 settings properties — 14× the façade threshold — and aggregates every deferred subsystem. |

**Moved *into* the wave lists (the never-move list was written before B2 landed):**

| File | Why it can move now |
|---|---|
| `Models/WordBorderInfo.cs` | Already the portable projection of the `WordBorder` control — flattening it to data is the class's whole job. Its only WPF tie is `Rect BorderRect` → `RectangleF`. The `WordBorderInfo(WordBorder)` constructor stays in the app as a factory. **This unblocks `ResultTable`'s clustering algorithm.** |
| `Models/TemplateRegion.cs` | Same shape: WPF-bound only through `ToAbsoluteRect`/`FromAbsoluteRect` returning `System.Windows.Rect`. **This unblocks `GrabTemplate` and `OcrDirectoryOptions`.** |

**Re-scoped:**
- `Utilities/PdfDocumentRenderer.cs` moves from Wave 4 to Wave 5. Its blocker is the
  `BitmapSource` currency (`RenderPageAsync` *returns* one) — identical to `ImageMethods`, not
  OCR-specific.
- `Utilities/AutomationProfile.cs` and `AutomationSettingsProvider.cs` go to **plain Core**, not
  Core.Windows. Verified empirically: with a `System.Configuration.ConfigurationManager` package
  reference, `ApplicationSettingsBase` / `LocalFileSettingsProvider` resolve *and run* on plain
  net10.0. `AutomationProfile` needs one mechanical change — widen
  `ApplySeed(Properties.Settings)` to `ApplySeed(ApplicationSettingsBase)`; every member it
  touches is on the base class. `AutomationSettingsProvider` needs no changes at all.
- `Utilities/Hdr/HdrToneMapper.cs` goes to **plain Core** — it uses nothing but `System`.

**Diagnoses corrected:**
- `ResultTable`'s `OcrResult` coupling is *dead code*, not a blocker. Its live path already
  consumes the portable `IOcrLinesWords`. The real blocker was `WordBorderInfo` — now resolved.
- `TesseractHelper`'s settings write-back needs no redesign. `ITextGrabSettings.Save()` already
  covers it. Its actual blocker is `AutomationProfile`, via one method (`TempImagePath`).
- `Utilities/Hdr/*` was recorded as "mostly clean already". It is not: `HdrScreenCapture.cs:472`
  reaches `System.Windows.Application.Current?.Dispatcher` to pump a consent dialog.

### 4.1 Wave 1 — shared leaves (do this first)

**This batch did not exist in the original plan and is now the highest-leverage work in it.**
Reconnaissance found the same handful of tiny files blocking Waves 3, 5 and 6 independently.
Until they land, every later batch hits the same wall: Core.Windows cannot reach back into the
app, so a leaf left behind blocks everything above it.

**1a — shared leaves → Core** (all verified dependency-free):
`Utilities/Singleton.cs`, `Utilities/StreamWrapper.cs` (class `WrappingStream`),
`Models/NullAsyncResult.cs` (`StreamWrapper` constructs it — same commit),
`Utilities/Json.cs` (namespace is `Text_Grab.Helpers`, not `.Utilities` — keep it),
`Utilities/IoUtilities.cs` *(pure string-list half only; the class also has WinForms and
Wpf.Ui `MessageBox` calls that stay behind)*.

**1b — packaging identity → Core.Windows.** Extract `AppUtilities.IsPackaged()` and
`GetAppVersion()` into `Text-Grab.Core.Windows/Utilities/PackageIdentity.cs`. Both need only
`Windows.ApplicationModel.Package`. They are unreachable today purely because they share a class
with `TextGrabSettings`/`TextGrabSettingsService`, which must stay in the app. Leave `AppUtilities`
forwarding so the ~dozens of existing call sites need not all change at once.

`StorageFileExtensions.cs` → Core.Windows also belongs here rather than in Wave 3 — it is
dependency-free and `SoftwareBitmapExtensions` needs it.

**Why this ordering matters:** 1a+1b unblocks, at minimum, `SettingsStorageExtensions` (3b),
`SoftwareBitmapExtensions` (5), `FileAssociationUtilities` (3a), `WinAiLanguageModel` (3c),
`FileUtilities` (5), and `CameraCaptureUtilities`'s `IsPackaged` call.

### 4.2 Wave 2 — pure leaves → Core

**2a — Enums.** Merge all 17 enums from `Text-Grab/Enums.cs` into `Text-Grab.Core/Enums.cs`.
Verified: all 17 are plain int/short-backed with no attributes, and there are **zero** name
collisions with Core's existing two. Do this early — it is a shared file every later batch may
otherwise touch.

**2b — Models (11 files, verified clean):** `AsyncOcrFileResult`, `EditTextTableDocument`,
`ExtractedPattern`, `FindResult` (calls a static on `EditTextTableDocument` — same commit, that
order), `GrabFrameTableEditState`, `GrabFrameWordGroupingMode`, `SpreadsheetUndoHistory`,
`TemplatePatternMatch`, `TemplateRecognizerMatch`, `ThirdPartyPackageInfo`.

**2c — Utilities / Extensions / Interfaces (9 files):** `PatternExecutor` then
`ColumnSplitUtilities` (which calls it — same commit, that order); `NumericUtilities`,
`LanguageHeuristics`, `Extensions/NumberExtensions`, `Extensions/StringBuilderExtensions`,
`Interfaces/ITtsEngine`.

Two files here need a **split**, not a move:
- `ProtocolUtilities` — only `IsProtocolUri` and `TryParseProtocolUri` are pure. The rest needs
  Registry + `AutomationProfile` + `FileUtilities`.
- `ThirdPartyNoticeUtilities` — only `Packages` and the constants are pure. The `Get*Path`/`Open*`
  methods need `FileUtilities.GetExePath()`. All three of its existing tests touch only
  `Packages`, so the test moves with the pure half.

**2d — geometry conversions.** Convert `WordBorderInfo.BorderRect` and
`TemplateRegion.ToAbsoluteRect`/`FromAbsoluteRect` to `RectangleF`, move both to Core, leave the
`WordBorderInfo(WordBorder)` factory in the app. Then `GrabTemplate` and `OcrDirectoryOptions`
follow. This is the batch that pays off B2.

**2e — CalculationService.** All three files, ~2000 lines, fully pure (one call into
`NumericUtilities`). **Move** the `NCalcAsync` and `UnitsNet` package references to
`Text-Grab.Core.csproj` — verified they are used nowhere else in the app. Leave `Tests.csproj`'s
own `NCalcAsync` reference alone. Move `CalculatorTests` and `UnitConversionTests` to `Tests.Core`
in the same commit.

**2f — Markdown split.** ~150 pure lines out of 1168. Pure half (Markdig AST + regex + string):
`LooksLikeMarkdown`, `ShouldPromoteLiveBlock`, `ShouldPromoteLiveMarkdown`, `NormalizeDocumentText`,
`NormalizeNewlines`, `EscapeMarkdownText`, `EscapeLinkDestination`, `ApplyQuotePrefix`,
`GetQuotePrefix`, `GetOrderedListStart`, `ResolveContentSpan`, `GetSourceSlice`,
`GetCodeSpanContentRawStart`, `GetCodeBlockText`, the `MarkdownPipeline` field and the three
`[GeneratedRegex]` methods. Everything touching `FlowDocument`/`System.Windows.Documents` stays.
The shared string helpers must become `internal`/`public` for the app half to call them. **Markdig
stays referenced by both halves** — the app side pattern-matches on Markdig types directly; verify
transitivity before removing the app's reference. Extract the 6 pure test methods into
`Tests.Core/MarkdownParsingTests.cs`.

### 4.3 Wave 3 — Windows leaves → Core.Windows

**3a — eight files, zero blockers, one commit.** `NativeMethods.cs`, `RegistryMonitor.cs`
(namespace is `RegistryUtils`, vendored — keep it), `OSInterop.cs`,
`DesktopNotificationManagerCompat.cs`, `Models/GeneratedOcrLinesWords.cs` (uses
`Windows.Foundation.Rect` — WinRT, not a B2 blocker), `Models/UiAutomationLang.cs`,
`Models/WindowsAiLang.cs`, `Models/WindowsAiDescriptionLang.cs`.

> `OSInterop.cs` is 1292 lines and was recorded as probably app-bound. `System.Windows.Forms`
> appears **once** in the whole file — in a `GetAsyncKeyState(Keys)` overload with zero callers.
> The only live caller uses the `int` overload. **Delete the dead overload and the file moves.**

**3b — after Wave 1.** `Extensions/SettingsStorageExtensions.cs` (namespace is `Text_Grab.Helpers`
— keep it; needs `Json.cs` from 1a). `Utilities/LimitedAccessFeatureUtilities.cs` (zero deps —
move first in the WinAI chain).

**3c — Windows AI chain, in this order:** `WinAiLanguageModel.cs` (needs `PackageIdentity` from 1b,
`OSInterop` from 3a, `LimitedAccessFeatureUtilities` from 3b), then `WinAiTranslator.cs` and
`WinAiMeetingNotes.cs` (both need only `WinAiLanguageModel`).

**3d — `Extensions/LanguageExtensions.cs` split.** `XmlLanguage` comes from `PresentationCore`
and the path is reachable in production from `GrabFrame`, `EditTextWindow` and `OcrUtilities` —
not a dead using. Move `IsSpaceJoining` (both overloads), `IsLatinBased`, `AsLanguage`,
`AsILanguage`; leave `IsRightToLeft(this Language)` and the `GlobalLang` branch of the `ILanguage`
overload in a thin app-side façade.

### 4.4 Wave 4 — OCR pipeline

**4a — `OcrOutput` → `BarcodeUtilities`, a move-now pair.** `OcrOutput.CleanOutput()` needs a
two-line swap to `SettingsAccess.Current` (`CorrectToLatin`, `CorrectErrors` — both already on the
interface) and the `is not Settings userSettings` cast dropped. `BarcodeUtilities` follows
immediately; add `ZXing.Net.Bindings.Windows.Compatibility` to Core.Windows.

**4b — `TesseractHelper`.** Blocked on `AutomationProfile` (via `TempImagePath` only) — so this
follows Wave 6a. Add `CliWrap` to Core.Windows. `TesseractGitHubFileDownloader` in the same file is
fully portable and could go to plain Core.

**4c — the `OcrUtilities` split. Opus owns this; do it as its own commit.**
`DefaultSettings` reads exactly six properties, all already on `ITextGrabSettings` — verified, no
seventh. The hidden WPF dependency is `LoadBitmapFromFile` (lines 887–899), which builds a
`BitmapImage` to apply EXIF rotation; decoupling it means a real rewrite against GDI+/WIC, so
**defer that method and its two callers** (`OcrAbsoluteFilePathAsync`, `OcrFile`) rather than
attempt it inside 4c.

**Ordering constraint the original plan missed: 4c cannot precede Wave 3.** `OcrEngine.cs` will
not compile in Core.Windows until `WindowsAiLang`, `WindowsAiDescriptionLang`, `UiAutomationLang`
(3a), `WinAiLanguageModel` (3c) and `AutomationProfile` (6a) have landed. If those are not ready,
move only the strictly portable subset — furigana filtering, paragraph-wrap heuristics,
`BuildTextFromOcrLines`, `GetStringFromOcrOutputs`, `GetTextFromOcrLine` — and leave engine
dispatch behind.

**Blocker created by batch 3d - RESOLVED in 4c.** `BuildTextFromOcrLines` calls
`language.IsRightToLeft()`, and 3d had left both `IsRightToLeft` overloads in the app as
`LanguageRtlExtensions` because `XmlLanguage` comes from PresentationCore.

Option 1 was taken, after settling the behaviour question empirically rather than by reasoning: a
throwaway WPF probe compared `XmlLanguage.GetLanguage(tag).GetEquivalentCulture().TextInfo
.IsRightToLeft` against `CultureInfo.GetCultureInfo(tag).TextInfo.IsRightToLeft` across 24 tags -
`ar`, `ar-EG`, `ar-SA`, `he`, `he-IL`, `ur`, `ur-PK`, `fa`, `fa-IR`, `ckb`, `ps-AF`, `sd-Arab-PK`,
`yi`, `he-Hebr-IL`, `ar-XX`, `en`, `en-US`, `ja`, `zh-Hans`, `de-DE`, and the unresolvable `xx`,
`xx-YY`, `und` and `""`. They agreed on every one, including the tags with subtags and the ones
neither can resolve.

So the `ILanguage` overload moved into Core.Windows `LanguageExtensions` with a `CultureInfo`
lookup in its `GlobalLang` branch, guarded by a `CultureNotFoundException` catch returning false
(XmlLanguage fell back to the invariant culture, which is LTR). That left the `Language` overload
with **zero** call sites - all five live `IsRightToLeft` calls are on `ILanguage` - so
`Extensions/LanguageRtlExtensions.cs` was deleted outright. 3d's facade is gone.

**4c as executed.** The split went the direction the call-site census pointed: the portable text
assembly - `GetTextFromOcrLine`, `FilterFurigana`, `FilterFuriganaLines`, `OrderLinesForReadingFlow`,
`BuildTextFromOcrLines`, `ShouldUseParagraphDetection`, `GroupWrappedParagraphLines`, `IsWrappedLine`,
`IsWrappedParagraph`, `GetStringFromOcrOutputs`, `ParseOcrResultIntoWordBorderInfos`, and the nested
`PositionedOcrLine`/`GroupedOcrLines` - moved to Core.Windows **keeping the `OcrUtilities` name**,
because `Tests/OcrTests.cs` alone held 43 of the file's ~80 references and every one of them is
against that subset. The app-coupled half - capture, engine dispatch, file and `BitmapSource`
sources - took the new name `OcrSourceUtilities`. `GetBoundingRect(this OcrLine)` was deleted as
section 8 dead code. Engine dispatch stayed behind as the ordering note above predicted:
`WindowsAiUtilities` and `LanguageUtilities` have not moved.

`Tests/OcrTests.cs` is the heaviest consumer of the headless surface and becomes a
`Tests.Core.Windows` candidate in Wave 7. It references the nested `PositionedOcrLine` /
`GroupedOcrLines` types by name; both halves keep `Text_Grab.Utilities`, so it stays green.

**4d — `ResultTable`.** Unblocked by 2d. Delete the dead code first (see §9), then move the
clustering algorithm.

**4e — language chain, strictly ordered, and hard-blocked at the end.**
`CaptureLanguageUtilities` → `LanguageUtilities` → `LanguageService`. The first two are pure
forwarders and move for free once the third does. `LanguageService` has a genuine blocker:
`System.Windows.Input.InputLanguageManager`, with no portable substitute. It also needs
`UiAutomationEnabled` and `WindowsAiDescriptionEnabled` added to `ITextGrabSettings`. **Route to
Opus** — the workable split is to extract the pure `switch`-expression helpers (`GetLanguageTag`,
`GetLanguageKind`, `GetPersistedLanguageIdentity`, `NormalizePersistedLanguageIdentity`) and leave
the input-language reader in the app.

**Ordering constraint found while preparing 4c: 4e cannot precede 5a.** Reading `LanguageService`
in full, `InputLanguageManager` appears in exactly one place - the private
`GetCurrentInputLanguageTag()` - and everything else in the class is WinRT, which is legal in
Core.Windows. That makes the better split the whole class moving under its own name with the
input-language read behind a resolver the app registers (the `SettingsAccess` shape), defaulting
to `CultureInfo.CurrentUICulture.Name` when none is registered. But `GetAllLanguages()` and
`GetOCRLanguage()` both call `WindowsAiUtilities.CanDeviceUseWinAI()`, and `WindowsAiUtilities` is
still app-side, deferred on `SoftwareBitmapExtensions` - which is 5a. Run 4e after wave 5.

**4e as executed, after wave 5.** `WindowsAiUtilities` moved first - its three blockers were all
gone (`AutomationProfile` in 6a, `SoftwareBitmapExtensions` in 5a, and `OverrideAiArchCheck` added
to `ITextGrabSettings` here). Its one remaining app call, `AppUtilities.IsPackaged()`, is a plain
forwarder to `PackageIdentity.IsPackaged()`, which has been in Core.Windows since 1b.

That cleared the way for the whole language chain to move to Core.Windows **unsplit** -
`LanguageService`, `LanguageUtilities`, `CaptureLanguageUtilities`, all keeping their names, so
none of their 155 call sites needed an edit. `UiAutomationEnabled` and `WindowsAiDescriptionEnabled`
joined `ITextGrabSettings` as the table predicted. `Singleton<T>` was already in Core.

The `InputLanguageManager` blocker became `Text-Grab.Core/Services/InputLanguageAccess.cs`, the
third instance of the delegate-resolver shape after `SettingsAccess` and `UiThreadAccess`. The
`NullReferenceException` catch that guarded the read stayed on the app side of the seam, inside the
registered resolver, since that is the only side that knows InputLanguageManager exists. A null tag
- no resolver, or no input language - still falls through to `CultureInfo.CurrentUICulture` and then
to en-US, exactly as before. Extracting the switch helpers, which the paragraph above proposed,
turned out to be unnecessary.

### 4.5 Wave 5 — capture and imaging → Core.Windows

**5a — move-now (after Wave 1):**
- `Utilities/Hdr/HdrToneMapper.cs` → **plain Core** (nothing but `System`).
- `Utilities/Hdr/DisplayHdrInfo.cs` → Core.Windows; add `Vortice.Direct3D11`, `Vortice.DXGI`.
- `Extensions/ImageExtensions.cs` → Core.Windows (pure GDI+; `ExifRotate` is dead — see §9).
- `Utilities/ImageChangeDetector.cs` → Core.Windows; add `Magick.NET-Q16-AnyCPU`,
  `Magick.NET.SystemDrawing`.
- `Models/DragDataObject.cs` → Core.Windows, after deleting its dead `BitmapSourceToBitmap` (§9).
- `Extensions/SoftwareBitmapExtensions.cs` → Core.Windows (needs `StorageFileExtensions` and
  `WrappingStream` from Wave 1).

**5b — `HdrScreenCapture.cs`.** The D3D11/DXGI/WinRT pipeline is clean. Two blockers: add
`HdrBorderlessGranted` to `ITextGrabSettings`, and extract the `Application.Current.Dispatcher`
hop at line 472 behind a settable hook the app wires up at startup — it exists to pump a one-time
OS consent dialog and is load-bearing.

**5b as executed.** Both blockers cleared. `HdrBorderlessGranted` and `HdrCaptureCorrection` were
added to `ITextGrabSettings`; both already existed in `Settings.settings`, so neither needed a
`.settings` edit. The `Application.Current.Dispatcher` hop became
`Text-Grab.Core/Services/UiThreadAccess.cs` - the same delegate-resolver shape as `SettingsAccess`,
registered from an app-side `[ModuleInitializer]` so the Tests host is covered without an
`App.appStartup` call. `TryPost` returning false is exactly the old `dispatcher is null` branch,
and `_borderlessRequestStarted` is still set before the post either way, so a process with no UI
thread does not re-request on every capture.

With `HdrScreenCapture` in Core.Windows, 5c's deferred `CaptureScreenRegion` moved as well - into
`BitmapUtilities` as `internal`, since its only two callers (`GetRegionOfScreenAsBitmap`,
`GetWindowsBoundsBitmap`) stay in the app and Core.Windows already grants `InternalsVisibleTo`
to it. That row is out of section 7.

**5c — `ImageMethods.cs` split.** Headless half (→ Core.Windows): `PadImage`,
`CaptureScreenRegion`, `GetBitmapFromIRandomAccessStream`, `GetRotateFlipType(string)`. Everything
touching `BitmapImage`/`BitmapSource`/`CachedBitmap`/`InteropBitmap`/`Window`/`ImageSource` stays.
Add `HdrCaptureCorrection` to `ITextGrabSettings`. **`GetRegionOfScreenAsBitmap` stays behind for
now** — it calls `Singleton<HistoryService>.Instance.CacheLastBitmap`, and inverting that call is a
redesign (invariant 5). `GetWindowsBoundsBitmap` is permanently app-bound; it pattern-matches on
the `GrabFrame` *View*.

**5d — `ClipboardUtilities.cs` split.** Larger than expected in the right direction: ~330 of 464
lines are a pure CF_HTML table parser with no clipboard, WPF, WinRT or GDI+ dependency →
**plain Core** as `Utilities/CfHtmlTableUtilities.cs`. The clipboard-touching methods stay.
Separately, line 64's `System.Windows.Forms.DataFormats.Bitmap` is the file's only WinForms use
and is the identical string constant to WPF's `System.Windows.DataFormats.Bitmap` — swap it in the
same commit regardless of whether the split happens.

**5e — `FreeformCaptureUtilities.cs`.** Only `CreateMaskedBitmap` moves, after changing its
parameter from `IReadOnlyList<Point>` to `IReadOnlyList<PointF>`; the single call site in
`FullscreenGrab.SelectionStyles.cs` converts via `AsPointF`. `GetBounds` and `BuildGeometry`
return WPF rendering types (`PathGeometry`) and stay.

**5f — `PdfDocumentRenderer.cs`** (re-scoped here from Wave 4). Blocked on the same `BitmapSource`
currency as `ImageMethods`: `RenderPageAsync` returns one, and changing that is a public API shape
change affecting multiple views. Its internal geometry and line-grouping logic is already portable
(`Windows.Foundation.Rect`) if partial credit is wanted.

### 4.6 Wave 6 — services and settings

**6a — settings providers → plain Core.** `AutomationSettingsProvider.cs` (no changes) and
`AutomationProfile.cs` (widen `ApplySeed` to `ApplicationSettingsBase`). Add
`System.Configuration.ConfigurationManager` to `Text-Grab.Core.csproj`. Highest-confidence batch in
the wave, and it unblocks `TesseractHelper` (4b), `ContextMenuUtilities` and
`FileAssociationUtilities` (3a-deferred), and `FileUtilities` (5).

**6b — speech.** `Services/WindowsSpeechEngine.cs` → Core.Windows. `Services/TtsService.cs` →
plain Core, after resolving its `private ITtsEngine _engine = new WindowsSpeechEngine();` field
initializer — the app should register the default engine at composition, same shape as
`SettingsAccess`. Add `TtsSpeakWordLimit`, `TtsVoiceName`, `TtsSpeakingRate`.

**6c — `AudioTranscriptionUtilities.cs` → Core.Windows, wholesale.** 1115 lines, fully headless
(NAudio + Whisper.net, zero WPF, zero WinRT), with exactly **one** settings touchpoint:
`AudioTranscriptionModel`. Move `NAudio`, `Whisper.net`, `Whisper.net.Runtime` to Core.Windows.
(`IncludeTimecodesInTranscription` and `NotifyOnTranscriptionComplete` are consumed only in the
views, not in this file.) The cleanest single file in the whole reorganization — use it as the
anchor that proves Core.Windows can host NAudio/Whisper.

**6d — `WebSearchUrlModel` split**, exactly `PatternItem`/`PatternItemCatalog`-shaped: pure record
→ Core, static accessors stay in the app. No interface changes needed.

**6e — `HistoryService.cs`. Opus owns this; it is a second `OcrUtilities`.** A genuinely headless
JSON pipeline (`LoadHistoryAsync`, `LoadHistoryWithRecovery`, `WriteHistoryFiles`, the
`Normalize*` methods, `HistoryLanguageKindJsonConverter`) is interleaved with WPF menu building,
`GrabFrame`/`EditTextWindow` construction and a GDI+ `CachedBitmap`, sharing private state across
both halves. Blocked on `HistoryInfo`'s own `System.Windows.Rect PositionRect` — which B2 and 2d
now give a path to.

### 4.7 Wave 7 — tests and closeout

**7a — test migration.** To `Tests.Core`: `StringMethodTests`, `TextSearchUtilitiesTests`,
`RecognizerExecutorTests`, `PatternExecutorTests`, `CalculatorTests`, `UnitConversionTests`,
`ExtractedPatternTests`, `ColumnSplitUtilitiesTests`, `SpreadsheetUndoHistoryTests`,
`EditTextTableDocumentTests`, `GrabFrameTableEditStateTests`, `ThirdPartyNoticeUtilitiesTests`,
plus the pure halves of `ProtocolUtilitiesTests` and `MarkdownDocumentUtilitiesTests`. To
`Tests.Core.Windows`: `OcrTests` and the other headless-Windows suites. Delete
`Tests.Core/ScaffoldingSmokeTests.cs`.

**7b — closeout.** Remove dead app-side shims; confirm the MSIX package still builds in Visual
Studio (the one thing the CLI gate cannot check); re-derive the never-move list one final time
against B2; update this document with the final layer map.

### Never moves — verified

`Views/`, `Controls/`, `Pages/`, `Styles/`, `Themes/`, `App.xaml.cs`, `AssemblyInfo.cs`,
`WPFExtensionMethods.cs`, `Properties/Settings.Designer.cs`, `TextGrabNotificationActivator.cs`.

**Extensions:** `ControlExtensions`, `DapploExtensions`, `KeyboardExtensions`, `ShapeExtensions`.

**Utilities:** `ColorHelper`, `CursorClipper`, `WindowResizer`, `WindowUtilities`,
`GrabFrameViewScaleUtilities`, `NotificationUtilities`, `WindowSelectionUtilities`, `HotKeyManager`,
`AutomationDiagnostics`, `NotifyIconUtilities`, `OutputUtilities`, `ShareTargetUtilities`,
`ImplementAppOptions`, `MagickHelpers`, `CameraCaptureUtilities`, `SettingsImportExportUtilities`,
`DiagnosticsUtilities`, `PostGrabActionManager`, `CustomBottomBarUtilities`, `ShortcutKeysUtilities`,
`UIAutomationUtilities` (see B4).

**Models:** `ButtonInfo` (~90 static entries each assigning `Wpf.Ui.Controls.SymbolRegular` —
whole-class, not splittable), `ShortcutKeySet` (`System.Windows.Input.Key`), `PostGrabContext`,
`FullscreenCaptureResult`, `UiAutomationOptions`, `UiAutomationOverlayItem`,
`UiAutomationOverlaySnapshot`, `WindowSelectionCandidate`, `LookupItem`.

**UndoRedoOperations:** all of them — `Operation`, `AddWordBorder`, `RemoveWordBorder`,
`ChangedImage`, `UndoRedo`, `ChangeWord`, `ResizeWordBorder`. Every one is typed on `WordBorder`,
`Canvas` or `ImageSource`.

### Consolidated `ITextGrabSettings` additions

Nine members across the whole plan, taking the interface from 10 to 19. Add each one only when its
batch runs.

| Property | Type | Needed by | Batch |
|---|---|---|---|
| `OverrideAiArchCheck` | `bool` | `WindowsAiUtilities` | 3 (deferred) |
| `UiAutomationEnabled` | `bool` | `LanguageService` | 4e |
| `WindowsAiDescriptionEnabled` | `bool` | `LanguageService` | 4e |
| `HdrCaptureCorrection` | `bool` | `ImageMethods` | 5c |
| `HdrBorderlessGranted` | `bool` | `HdrScreenCapture` | 5b |
| `AudioTranscriptionModel` | `string` | `AudioTranscriptionUtilities` | 6c |
| `TtsSpeakWordLimit` | `int` | `TtsService` | 6b |
| `TtsVoiceName` | `string` | `WindowsSpeechEngine` | 6b |
| `TtsSpeakingRate` | `double` | `WindowsSpeechEngine` | 6b |

All nine already exist in `Settings.settings`, so each is a one-line interface addition with no
`.settings` edit. Declined: the three `UiAutomation*` traversal properties, per B4.

**The `Load*`/`Save*` families are not candidates.** `LoadStoredRegexes`, `LoadBottomBarButtons`,
`LoadWebSearchUrls` and friends are `SettingsService` *methods*, not scalar properties. They do not
fit this interface's shape, and the façade pattern (`PatternItemCatalog`) handles them with no
interface change at all.
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

Read D:\source\TheJoeFin\Text-Grab\docs\Core-Split-Plan.md first — sections 1 (especially
invariant 8), 2, and your batch in section 4 are binding. Then read the two most recent
move commits (git show edefeaa, git show e677b54) to match the established style.

Your file list is exactly:
  <paths>
Target project: <Text-Grab.Core | Text-Grab.Core.Windows>

Procedure, per file:
 1. Read it in full. Confirm which TYPES it uses — invariant 8 lists the four traps, and
    the original wave lists were wrong about a dozen files for exactly these reasons.
 2. If it moves cleanly: `git mv` it, keep the namespace, fix call sites the compiler flags.
 3. If it needs a split: pure part moves, the coupled façade stays in the app under a new
    name. See PatternItem/PatternItemCatalog in e677b54 for the shape.
 4. If it is blocked by something outside your list: LEAVE IT. Do not expand scope.
 5. If section 8 lists dead code in a file you are moving, re-verify it has no call sites
    (beware target-typed `new()`), then delete it as part of the move.

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

### What the reconnaissance pass actually bought

Worth recording, because it justifies doing this again before Waves 5 and 6 execute. Five
read-only agents ran in parallel against one working tree — safe because none of them could
write. Between them they:

- found `OSInterop.cs` (1292 lines) blocked by a single dead line;
- found the `AppUtilities.IsPackaged()` shared blocker that no single wave owned, which is now
  Wave 1;
- **disproved four entries** on the original wave lists and **rescued two** from the never-move
  list;
- settled the `System.Configuration`-on-net10.0 question by building and running a probe rather
  than reasoning about it;
- surfaced the `FrameworkReference` loophole in invariant 2 (§B4).

The cost was five agents reading ~60 files. The alternative was movers discovering each of these
mid-batch, with a half-applied commit in the tree.

**Verify before acting on a report.** Every load-bearing claim above was independently checked
before it entered this document, and one was wrong in a way that mattered: an agent reported
`GrabFrame` constructing a `ResultTable`, and a naive `grep "new ResultTable"` appeared to refute
it — the call is target-typed `new()`. Trusting the grep over the agent would have deleted a live
constructor.

## 6. Risk register

| Risk | Mitigation |
|---|---|
| MSIX packaging breaks (CLI gate can't see it) | Open the solution in VS and build the wapproj at each wave boundary. |
| A mover flips `UseWPF=true` on Core.Windows to unblock itself | `Tests.Core.Windows/TierBoundaryTests.cs` fails the build. Also check the csproj diff in review. |
| Silent behavior change from a "cleanup" during a move | Movers are told to change only what the compiler flags. Review diffs for unrequested edits. |
| `RuntimeIdentifiers` dropped from a library csproj | Causes NETSDK1047 in the wapproj restore only — invisible to the CLI gate. Grep the csprojs at wave boundaries. |
| Settings interface sprawls to all 104 properties | Add properties only when a move demands one; if a batch needs more than ~5 new ones, that file probably wants a façade split instead. |
| `EditTextWindow.xaml.cs` / `GrabFrame.xaml.cs` churn | They are touched by most batches. Serial movers make this safe; parallel ones would not. |
| A mover classifies by `using` lines and moves a WPF-bound file | Invariant 8. `TierBoundaryTests` catches it at the assembly level if it slips through. |
| A batch stalls because a one-line leaf it needs is still app-side | Wave 1 exists precisely for this. Do not start Waves 3–6 before it lands. |
| Deleting "dead" code that is actually reachable | §8 rows were each verified by full-repo grep. Re-verify before deleting; target-typed `new()` and same-named members on other types defeat a naive grep — that is how `GrabFrame`'s `ResultTable` construction was nearly missed. |

## 7. Deferred ledger

Files with a real blocker, and the specific thing that clears it. **Movers append here.**
Every row below was verified by reading the file, not inferred.

| File | Blocker (specific) | Unblocked by |
|---|---|---|
| `Utilities/OcrSourceUtilities.cs` | Post-4c remainder. `LoadBitmapFromFile` builds a WPF `BitmapImage` to apply EXIF rotation; decoupling means a GDI+/WIC rewrite, and it takes `OcrAbsoluteFilePathAsync` and `OcrFile` with it. Engine dispatch additionally needs `WindowsAiUtilities` (5a) and `LanguageUtilities` (4e); the rest is `Window`/`BitmapSource` capture and stays | a GDI+/WIC rewrite, then 5a + 4e |
| `Utilities/TesseractHelper.cs` | `TempImagePath()` calls `AutomationProfile.GetTemporaryDirectory()`. The settings write-back is **not** a blocker — `ITextGrabSettings.Save()` already covers it | 6a |
| `Utilities/ContextMenuUtilities.cs` | `AutomationProfile.Current`, `FileUtilities.GetExePath()`; `IoUtilities` mixes pure extension lists with WinForms/Wpf.Ui `MessageBox` calls | 1a (IoUtilities split), 5, 6a |
| `Utilities/FileAssociationUtilities.cs` | `FileUtilities.GetExePath()` | Wave 5 |
| `Utilities/FileUtilities.cs` | `AutomationProfile.Current` in 6 methods | 6a |
| `Utilities/GrabFrameFileUtilities.cs` | Public signature bound to `HistoryInfo`, which needs B2 (`Rect PositionRect`), 2a (five enums) and 4e. A façade was considered and rejected as larger than the file it wraps | 2d + 4e, or `HistoryInfo` moving |
| `Services/SettingsService.cs` | Clones `ButtonInfo` and `ShortcutKeySet` field-by-field (both never-move); `Windows.Storage.ApplicationDataContainer` caps it at Core.Windows regardless | needs a `ButtonInfo` redesign — likely never |
| `Utilities/GrabTemplateManager.cs` | `SaveTemplateReferenceImage` (BitmapSource) and `CreateButtonInfoForTemplate` (Wpf.Ui) must stay; `IsFileBackedManagedSettingsEnabled` is a service property, not a scalar. `GrabTemplate`/`TemplateRegion` moved to Core in 2d, so the remaining blocker is a plain split | a split |
| `Utilities/GrabTemplateExecutor.cs` | `LoadStoredRegexes()` needs a non-scalar seam. `GrabTemplate`/`TemplateRegion` moved to Core in 2d and 4c has landed, so the remaining blocker is the settings façade plus its calls into `OcrSourceUtilities` | a façade + `OcrSourceUtilities` |
| `Utilities/PdfDocumentRenderer.cs` | `RenderPageAsync` returns `BitmapSource` — a public API shape change affecting several views | 5f |
| `Services/HistoryService.cs` | Headless JSON pipeline interleaved with WPF menu building and `GrabFrame`/`EditTextWindow` construction, sharing private state | Opus split (6e) |

## 8. Verified dead code — free, zero-risk prep

Each of these was confirmed to have **zero call sites** across the repo. Deleting them is safe
and independent of any wave; two of them unblock real moves.

| Dead code | Why it matters |
|---|---|
| `OSInterop.GetAsyncKeyState(System.Windows.Forms.Keys)` (line 125) | The **only** `System.Windows.Forms` reference in all 1292 lines. Deleting it moves the whole file. |
| `Models/DragDataObject.BitmapSourceToBitmap` (line 77) | The file's only WPF touchpoint, and a duplicate of `ImageMethods.BitmapSourceToBitmap`. Deleting it moves the file. |
| `Models/ResultTable`: `OcrResult` property, `ParseOcrResultWordsIntoRects()`, the `ResultTable(ref List<WordBorderInfo>, DpiScale)` ctor, `CalculateResultRows`, `MergeTheseRowIDs` | Leftovers from a superseded grid-line algorithm. The `OcrResult` property is why `ResultTable` looked WinRT-coupled; the live path already uses `IOcrLinesWords`. |
| ~~`Utilities/OcrUtilities.GetBoundingRect(this OcrLine)`~~ | Deleted in 4c. `edefeaa` had left it saying "other app code may still use it"; nothing did. |
| `Extensions/ImageExtensions.ExifRotate` (line 12) | Unused. |

Not dead, but a one-line dependency removal in the same spirit:
`ClipboardUtilities.cs:64` uses `System.Windows.Forms.DataFormats.Bitmap` — the identical string
constant to WPF's `System.Windows.DataFormats.Bitmap`, and the file's only WinForms use.

## 9. Definition of done

- `Text-Grab.Core` holds the text, pattern, table, calculation, and template logic, with no
  `System.Windows`, no `Windows.*`, no P/Invoke.
- `Text-Grab.Core.Windows` holds OCR engines, capture, imaging, Windows AI, and Win32 interop,
  with `UseWPF=false` still set.
- `Text-Grab` holds Views, Controls, Pages, app wiring, and thin adapters — nothing else.
- `Tests.Core` runs in ~2s with no display and covers the pure tier; `Tests` keeps only the
  WPF/STA tests.
- CI runs all three test projects; the MSIX package builds in Visual Studio.
- Section 7 is empty, or every remaining row has a written reason it stays.
