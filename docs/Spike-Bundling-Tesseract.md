# Spike: bundling Tesseract with the packaged (MSIX) and unpackaged app

Status: spike complete, nothing changed in the app. Findings below were produced with a throwaway
console PoC and a throwaway worktree (both discarded), on a Windows 11 ARM64 machine, .NET SDK 10.0.404.

## Question

Today Text Grab does not ship Tesseract. `TesseractHelper` (Text-Grab.Core.Windows) looks for a
user-installed `tesseract.exe` (`%LOCALAPPDATA%\Tesseract-OCR`, `%LOCALAPPDATA%\Programs\Tesseract-OCR`,
`C:\Program Files\Tesseract-OCR`, or the `TesseractPath` setting), shells out to it with CliWrap, and
lists languages with `--list-langs`. How do we ship Tesseract inside the app so the packaged build
(and later the unpackaged zips) work with no separate install?

## Recommendation

Use the **`Tesseract.CrossPlatform` 5.5.2.200** NuGet package (in-process wrapper) in
`Text-Grab.Core.Windows`. It pulls `Tesseract.Native`, which carries the native DLLs for **win-x64 and
win-arm64** (x86 is out of scope). The MSIX picks the DLLs up with no packaging changes. The unpackaged
single-file build needs a small loader workaround (see "Single-file gotcha"). Language data
(`.traineddata`) lives in a `tessdata` folder next to the settings: the package's local folder for MSIX,
next to the exe for unpackaged (see "Language data"). New users get only the bundled engine; users who
already configured an external `tesseract.exe` keep working unchanged, and `UseTesseract` stays off by
default (see "Engine policy").

Runner-up if we want the smallest code change: **`NAPS2.Tesseract.Binaries` 1.5.0**, which ships a
self-contained `tesseract.exe` per architecture (win32/win64/winarm, 4.6 to 5.4 MB each, no DLLs) and
keeps the existing CliWrap flow. Not prototyped; the Tesseract version it bundles was not checked.

## Options evaluated

| Package | Native DLLs | win-arm64 | Verdict |
|---|---|---|---|
| `Tesseract.CrossPlatform` 5.5.2.200 + `Tesseract.Native` | yes (runtimes/win-x64, win-arm64) | yes | **Recommended.** Tesseract 5.5.2, Leptonica 1.87.0, Apache-2.0, ships third-party license texts |
| `Tesseract` (charlesw) 5.2.0 | yes (`x86/`, `x64/` folders) | **no** | Rejected: no ARM64, Tesseract 5.0-era, stale |
| `TesseractOCR` (Sicos1977) 5.5.2 | yes (`x86/`, `x64/`) | **no** | Rejected: no ARM64 |
| `NAPS2.Tesseract.Binaries` 1.5.0 | none, static `tesseract.exe` | yes (`_winarm`) | Viable fallback, exe-based, minimal code change |
| Bundle the UB-Mannheim install by hand | exe + ~dozens of DLLs | x64 only | Rejected: manual, x64-only (the installed one here runs emulated on ARM64) |

ARM64 matters: the app ships `win-arm64` builds and the dev machine is ARM64.

## Findings

### 1. The in-process wrapper runs natively on ARM64
PoC console app (`net10.0`, `win-arm64`) with `eng.traineddata` from `tessdata_fast`:
engine init ~50 to 190 ms, OCR of `Tests/Images/font_sample.png` ~40 to 60 ms, mean confidence 0.90.

### 2. DLL set and size (per architecture)
`Tesseract.Native.runtime.win-<arch>` contributes 26 native DLLs, 20.5 MB raw, 9.4 MB zipped.

The wrapper loads `tesseract.dll` and `leptonica.dll` by those exact names. `tesseract55.dll` and
`leptonica-1.87.0.dll` are byte-identical copies; `leptonica-1.87.0.dll` is a real import of
`tesseract.dll`, `tesseract55.dll` is not.

I removed each DLL individually and re-ran. Individually optional: `legacy.dll`, `libssl-3-*.dll`,
`libwebpdecoder.dll`, `libwebpdemux.dll`, `tesseract55.dll`, `turbojpeg.dll`. Removing all six together
still worked (20 files, 16.1 MB raw, 7.5 MB zipped, so about 4.4 MB raw / 1.9 MB zipped saved per arch).
Everything else is a hard dependency, including `libcurl`, `archive`, `libcrypto`, `zstd`, `lzma`,
`lz4`, `bz2` (Tesseract 5.5 is built against libarchive and curl).

Trimming is optional and low value. Caveats: removing `legacy.dll` was only tested with the default
engine mode, and the set can change on every package update. If done, do it with an explicit
deny-list target and re-run the smoke test.

### 3. MSIX: DLLs are included automatically
With the package reference added to `Text-Grab.Core.Windows`, a normal RID-specific build puts the
DLLs flat in the output root. I built the real package
(`MSBuild Text-Grab-Package.wapproj -p:Platform=ARM64 -p:AppxBundle=Never`) and listed the `.msix`:
`Tesseract.CrossPlatform.dll` and all native DLLs are under `Text-Grab/`, next to `Text-Grab.exe`.
No `Package.appxmanifest` or `.wapproj` edit is needed. The package was 143.1 MB; I did not build a
baseline MSIX, so the exact delta is not measured (expect roughly the 9.4 MB zipped figure per arch,
before the bundle compresses further).

What I did **not** do: install the MSIX and run OCR inside the packaged app. The packaged app is
full-trust (`runFullTrust`), so loading DLLs from the package folder is expected to work, but it is
unverified.

### 4. Single-file gotcha (unpackaged zips)
`Release.yml` publishes with `PublishSingleFile=true`, and the Windows App SDK targets force
`IncludeAllContentForSelfExtract=true` for single-file (`Microsoft.WindowsAppSDK.SingleFile.targets`).
So all natives, including the new ones, get embedded in `Text-Grab.exe` and extracted at launch to
`%TEMP%\.net\<app>\<hash>\`. Whisper's natives already ship this way.

Result in the PoC, published single-file with natives embedded:

| Mode | Result |
|---|---|
| Natives left beside the exe | works |
| Natives embedded, no workaround | **fails**: `DllNotFoundException: Failed to find library "tesseract.dll"` |
| Natives embedded + `TesseractEnviornment.CustomSearchPath` set to the extraction dir | works |

Workaround, run once before the first `new TesseractEngine(...)`:

```csharp
// The wrapper probes next to the assembly / app base dir, which is empty for embedded natives.
string dirs = (string?)AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") ?? string.Empty;
foreach (string dir in dirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
{
    if (File.Exists(Path.Combine(dir, "tesseract.dll")))
    {
        TesseractEnviornment.CustomSearchPath = dir; // sic: the property is misspelled in the package
        break;
    }
}
```

This is harmless in the packaged and non-single-file cases (the extraction dir simply is not found).
Not tested inside the real Text-Grab.exe, only in the PoC with the same publish mode.

### 5. Output parity with the installed `tesseract.exe` (partial)
Same `eng.traineddata` for both, default settings:

- `Tests/Images/font_sample.png`: **byte-identical**.
- Three UI screenshots (`ETW.png`, `GF.png`, `QSL.png`): **differ slightly** (for example one line
  `File Edit Help Grab Frame` came out as `File`). Forcing `PageSegMode.Auto` did not change it.
  The installed exe is 5.4.0 / Leptonica 1.84, the wrapper is 5.5.2 / 1.87, but I did not isolate the cause.

This needs a proper parity pass against the OCR fixtures before switching the default, ideally via
`Tests.Core.Windows/OcrTests.cs`.

### 6. Architecture coverage
Decision: **x86 is out of scope.** `Tesseract.Native` has no `win-x86` runtime, and releases only
build x64 and arm64 (wapproj bundle is `x64|arm64`). No x86 guard or fallback is planned.

## Language data (`.traineddata`)

The natives do not include models. **Decision: language data is stored alongside the app's settings,
using the same location rules `SettingsService` already uses**, in a `tessdata` folder:

| Install | tessdata folder | Same base as |
|---|---|---|
| MSIX (packaged) | `ApplicationData.Current.LocalFolder.Path\tessdata` (the package's local app data) | settings sidecar + managed JSON settings |
| Unpackaged EXE | `<folder containing Text-Grab.exe>\tessdata` | `settings` sidecar, which is next to the exe today (`FileUtilities.GetExePath()`) |
| UI automation profile | a `tessdata` folder under the profile directory | `AutomationProfile` settings dir (keeps tests isolated from the real user) |

Notes:
- The MSIX install folder is read-only, which is why packaged uses `LocalFolder`. It survives
  package upgrades like the existing settings do.
- The unpackaged location follows wherever the settings end up (they are currently portable next to
  the exe). If the exe sits in a read-only folder such as Program Files, language downloads fail the
  same way settings saves already would. Handle the failure with a clear message rather than a new fallback path.
- Implement as one helper (for example `TesseractHelper.GetTessdataFolder()`) next to
  `GetManagedJsonSettingsFolderPath()`/`GetRegularSettingsSidecarFilePath()` in `SettingsService`, so the
  three cases above are decided in one place. Create the folder on demand.
- `TesseractGitHubFileDownloader` already downloads from `tesseract-ocr/tessdata`; only the destination changes.
- **Language list:** `TesseractLanguagesAsStrings()` currently parses `tesseract --list-langs`;
  in-process it becomes enumerating `*.traineddata` in that folder (excluding `osd`, as today).
- **Decision: bundle English.** Ship `tessdata_fast/eng.traineddata` (~4 MB, from
  `tesseract-ocr/tessdata_fast`) as a content file in the app folder (read-only is fine). The app
  resolves a language by looking in the writable tessdata folder first, then the bundled folder, so a
  user-downloaded `eng` (for example the larger `tessdata` model) overrides the bundled one. Add the
  tessdata_fast license (Apache-2.0) to `ThirdPartyNotices`. Unpackaged single-file builds embed
  content files differently from natives, so verify the file is reachable next to the exe or copy it
  out on first run.
- **Everything else stays on-demand** via `TesseractGitHubFileDownloader`.

## Engine policy: bundled for new users, existing setups unchanged

**Decision.** New users can only use the bundled engine; they are never offered "use my own
install". Users who had already set up an external Tesseract keep working exactly as today (the
existing `tesseract.exe` + CliWrap path, their install's `tessdata`, their `TesseractPath`).
`UseTesseract` stays **off by default** (it already is: `Settings.settings` default `False`, and
`AutomationProfile` forces it off); nobody gets Tesseract OCR until they turn it on.

(Technical note, not exposed in the UI: I verified the bundled in-process engine can read another
install's folder. Pointed at `C:\Program Files\Tesseract-OCR\tessdata` it OCR'd the font sample
correctly. We deliberately do not offer this to new users, so the language/model story stays single-sourced.)

### Two engines, one setting that picks

Add one setting, `TesseractEngineMode` (string, default empty): `""` (not decided yet), `Bundled`, `External`.

**One-time decision, made on first launch of the new version, before anything reads Tesseract state:**
- If the saved `TesseractPath` is non-empty **and the file still exists** -> `External`.
- Otherwise -> `Bundled`.
- The decision is written once and never re-evaluated, so installing Tesseract later does not
  flip a new user to External, and updating Text Grab does not flip an existing user to Bundled.
- The migration must **not** run the existing probe (`GetTesseractPath` checks
  `%LOCALAPPDATA%\Tesseract-OCR`, `...\Programs\Tesseract-OCR`, `C:\Program Files\Tesseract-OCR` and
  writes what it finds into `TesseractPath` as a side effect). Running it would classify every user who
  merely has Tesseract installed as an existing user. Today that probe only ran when the Tesseract
  settings page loaded or Tesseract was in use, so a saved path is a good signal that the user engaged with it.
  In Bundled mode the probe must never run at all, or it would silently write a path later.
- Automation/UI-test profiles always get `Bundled`.

| | Bundled (new users) | External (existing users) |
|---|---|---|
| OCR | in-process `Tesseract.CrossPlatform` | `tesseract.exe` via CliWrap, unchanged |
| Languages | app tessdata folder + bundled English ("Language data") | the install's `tessdata`, unchanged |
| Language downloads | to the app tessdata folder, no UAC | unchanged (elevated copy into the install) |
| Settings UI | new page (below) | legacy path/Open Folder card + **Switch to built-in Tesseract** |

Rules for switching:
- An External user can switch to Bundled from the settings page. Offer **switching back** only while
  their saved `TesseractPath` still points at an existing file. A Bundled user can never reach External.
- If an External user's exe disappears (uninstall), do not silently switch engines: show the problem
  on the page with a one-click switch to built-in, and make OCR fail with a clear message as it does today.
- Switching does not touch `UseTesseract` or the language files of the other engine.

### Consequences for the code
- `TesseractHelper` becomes a thin facade over two backends: `ExternalExeBackend` (the current code
  moved as-is, treated as frozen/legacy, bug fixes only, keeps the `CliWrap` reference) and `BundledBackend`.
  Everything keys off the mode.
- Callers that gate on `UseTesseract && TesseractHelper.CanLocateTesseractExe()` today
  (`FullscreenGrab.xaml.cs:68`, `EditTextWindow.Menus.cs:388`, `CaptureLanguageUtilities`,
  `LanguageSettings`) must switch to a mode-aware `TesseractHelper.IsAvailable`. A bundled user has no
  exe to locate, so leaving `CanLocateTesseractExe` in place would make Tesseract look permanently missing.
- New setting touches `Settings.settings`, `ITextGrabSettings`, both test fakes
  (`Tests.Core.Windows/FakeTextGrabSettings.cs`, `Tests.Core/OcrOptionsTests.cs`), `AutomationProfile`
  (force `Bundled`) and `DiagnosticsUtilities` (report the mode).
- Models/languages may differ between engines: External users keep their install's languages, which are
  not visible to the bundled engine. Mention this in the switch confirmation so nobody loses a language by switching.
- Plan the eventual removal of External (for example after a few releases of telemetry-free, support-driven
  judgement); until then it is frozen, not extended.

Known limits of the bundled engine, for the Bundled-side languages UI:
- A single engine loads all its languages from **one** folder, and the app tessdata and bundled
  folders are two folders, so `eng+deu` only works when both files are in the same one. Untested here;
  this is how the Tesseract API works. Options: prefer the app tessdata folder, and when a combined
  spec needs a bundled file, copy `eng.traineddata` into the app folder first.
- Models must be 4.x/5.x LSTM-compatible; fail with a readable message otherwise.

## Tesseract settings page overhaul

The page has two forms, chosen by `TesseractEngineMode` ("Engine policy"). **Bundled** is the new page
described in the layout below. **External** keeps today's page (path box, Open Folder, status warning)
with two changes: it adds a **Switch to built-in Tesseract** button and drops the winget card text that
implies new installs. New users only ever see the Bundled form.

Today (`Pages/TesseractSettings.xaml(.cs)`) the page is built around one idea: find `tesseract.exe`.
It has an enable toggle that is force-disabled when the exe is not found, a "⚠️ Could not locate
Tesseract.exe" warning, a path text box + Open Folder button, a winget "How do I get Tesseract?"
card, and an About card that says Text Grab "will pass the image to the Tesseract EXE". Language
management lives on a different page (`Pages/LanguageSettings`): installed list, an "Open Tesseract
Language Data Folder" button, and a download picker that copies the file into
`<tesseract install>\tessdata` through an elevated `cmd /c copy` (UAC prompt, fails if there is no
install). All of that assumption goes away, so this page is redesigned rather than patched.

### Target layout

1. **Engine status card (new, top of page).** "Built-in Tesseract 5.5.2 (Leptonica 1.87.0)" with a
   state: *Ready*, *Library failed to load* (shows the exception message and a Copy details button,
   useful for the single-file and packaged unknowns in this spike), or *No languages available*.
   Computed off the UI thread by creating a `TesseractEngine` for the first available language.
   Also shows the Tesseract version string from `engine.Version`.
2. **Enable toggle.** Same `UseTesseract` setting and copy, **default off (decided)**, but no longer
   disabled because an exe is missing. It is disabled only when the engine status is not *Ready*. Remove
   the "Could not locate Tesseract.exe" warning (replaced by the status card). Turning it on is the only
   way a user starts using Tesseract; there is no auto-enable, including after the English model is bundled.
3. **Languages (moved here from the Language page).**
   - Installed list, one row per language: tag, display name, **source** badge (*Bundled*,
     *Downloaded*), file size.
   - Per-row **Remove** for *Downloaded* only. The bundled file is read-only (removing a downloaded
     `eng` falls back to the bundled one).
   - **Download** picker (existing combo over `tesseractTrainedDataFileNames`, ideally searchable and
     hiding languages already installed) with a progress bar, Cancel, and an inline result
     (success, or the actual error). No UAC prompt: files go to the user-writable tessdata folder.
   - **Open language data folder** and **Refresh** buttons.
   - Empty/loading states; nothing here blocks the UI thread.
4. **Language data folder (new, read-only row).** Shows the resolved path from the table in "Language
   data" with an Open button. For unpackaged installs, check write access on load and show a warning
   with the reason if the exe's folder is not writable (downloads would otherwise fail).
5. **No "Tesseract location" card in the Bundled form (decided).** New users cannot point Text Grab
   at their own install, so the path box, Open Folder button and exe validation do not exist there.
   The `TesseractPath` setting is kept untouched for External users only. In the **External** form this
   position holds the existing path card, plus a **Switch to built-in Tesseract** button with a
   confirmation that names the consequence (languages from their install are not used by the built-in
   engine) and, while the saved path is still valid, a **Switch back to my install** button.
6. **Get more accurate models (replaces "How do I get Tesseract?").** Remove the winget command card.
   Keep an informational note: the bundled English model is the fast one, larger/more accurate models
   come from the `tessdata` repo, and downloaded files override bundled ones. Optional stretch: a
   model quality choice (fast / standard / best) in the downloader, which changes the source repo URL.
7. **About card, rewritten.** Fix the stale "pass the image to the Tesseract EXE" text (it is now
   in-process, no temp file), say Tesseract is bundled, name the version, link to the project and
   the license notices. Re-verify the line "Does not use Tesseract: Table Recognition and the Grab
   Frame", I did not check that claim.

### Language page
In **Bundled** mode the "Tesseract languages" section of `LanguageSettings` is replaced by a single row
linking to the Tesseract page (the list, download picker and folder button now live there). In
**External** mode the section stays exactly as is (`InstallButton_Click`,
`CopyFileWithElevatedPermissions`, `OpenPathButton_Click`, `LoadTesseractContent`), marked legacy, so
existing users' language management keeps working. The language pickers elsewhere (Fullscreen Grab,
Edit Text Window) keep using `TesseractHelper.TesseractLanguages()`, which dispatches on the mode.

### Supporting changes the page needs
- **Downloader hardening (`TesseractGitHubFileDownloader`).** It currently swallows all exceptions
  and writes to the console, so the page cannot tell success from failure. Return a result (or throw),
  check the HTTP status, support `CancellationToken` and `IProgress<double>`, download to a `.tmp` file
  then move into place, and validate the result (non-empty, plausible size) so a failed download never
  leaves a corrupt `.traineddata` that breaks every OCR call. Existing `TesseractGitHubFileDownloaderTests`
  cover the current API and need updating.
- **Language service.** One component that owns the source list and returns, per language, the file
  path, source and size. The page, the language pickers, and `TesseractHelper` all use it, replacing
  `--list-langs`.
- **Change notification.** After a download, remove, or source change, invalidate language caches
  (the Language page already calls `LanguageUtilities.InvalidateAllCaches()` for similar changes) so open
  pickers update.
- **Settings.** One new setting, `TesseractEngineMode` ("Engine policy"); `UseTesseract` (default off)
  and `TesseractPath` keep their names and meaning. `Settings.settings` is shared by packaged and
  unpackaged builds (packaged mirrors it through `ApplicationDataContainer`), so no renames.
- **Accessibility/automation.** Keep existing `AutomationProperties.AutomationId` values where the
  control survives (`Settings.TesseractPage`, `Settings.Tesseract.EnabledToggle`,
  `Settings.Tesseract.Path`, `Settings.Tesseract.OpenFolderButton`) and add IDs for every new control
  (status, language list, download button, remove button, progress, data folder). The IDs
  `Settings.Languages.*Tesseract*` move with the controls or are retired; update
  `UiTests/Suites/Settings.Tests.ps1`, the `settings-tesseract`, `ocr-tesseract` and
  `language-tesseract-independent` entries in `UiTests/coverage.json`, and the `tesseract` capability
  probe. Those tests are currently skipped without an external install; with bundled English they can
  run on any machine, so drop the "requires Tesseract with English language data" reasons.
- **Tests.** Unit tests for the engine-mode migration (saved valid path -> External; empty path ->
  Bundled; saved path whose file is gone -> Bundled; Tesseract installed but no saved path -> Bundled and
  the probe is not run; decision is not re-evaluated on a second launch), the tessdata folder resolver
  (packaged, unpackaged, automation profile), language discovery and precedence, the hardened
  downloader, the unwritable-folder warning, and that `UseTesseract` defaults to false. A UI test that
  opens the page in Bundled mode and sees *Ready* with `eng` listed as *Bundled*; External-mode page
  tests need a seeded `TesseractPath`.

## Code that changes (not done in this spike)

- `Text-Grab.Core.Windows.csproj`: add `Tesseract.CrossPlatform`.
- `TesseractHelper.cs`: becomes a facade over `ExternalExeBackend` (existing CliWrap code, frozen) and a
  new `BundledBackend` using a cached `TesseractEngine` per language ("Engine policy").
  `TesseractEngine` is not thread-safe, so serialize access or pool. Feed pixels directly
  (`Pix.LoadFromMemory`) instead of writing `tempImage.png`.
- `TesseractEngineMode` setting + one-time migration, and mode-aware `TesseractHelper.IsAvailable`
  replacing the `UseTesseract && CanLocateTesseractExe()` gates.
- `TesseractSettings` page, `LanguageSettings` page and `TesseractPath` setting: full redesign, see
  "Tesseract settings page overhaul". Settings keys are shared across the unpackaged and packaged builds.
- Tessdata folder helper implementing the table in "Language data".
- Other files that reference Tesseract and need a look: `OcrEngineUtilities`, `LanguageService`,
  `CaptureLanguageUtilities`, `DiagnosticsUtilities`, `AutomationProfile`, and the UI-test
  `tesseract` capability (`UiTests/Suites/OcrCapability.Tests.ps1`, `coverage.json`), which currently
  skips when no external install exists.
- `ThirdPartyNotices`: add the license texts from the package's `licenses/` folder (tesseract,
  leptonica, libcurl, openssl, libarchive, libpng, libtiff, libwebp, zstd, and others; they are not
  copied to the output automatically). Add Tesseract to `BUILT-WITH.md`.

## Open items

1. Install the MSIX and confirm OCR works in the packaged app.
2. Run the real unpackaged single-file `Text-Grab.exe` with the loader workaround.
3. Parity pass against OCR fixtures (finding 5).
4. Verify the bundled `eng.traineddata` is reachable in the unpackaged single-file build, and test
   combined language specs across the app and bundled folders.
5. Measure the real MSIX and zip size delta against a baseline build.
6. Decide whether the downloader offers a fast / standard / best model choice.
7. Decide when (or whether) to retire the External engine, and whether the `TesseractPath`-based
   migration rule is the right "already set up" signal (edge: a user with a saved path who never
   enabled Tesseract is classified External; they can switch to built-in).

Decided: new users are Bundled only; existing setups continue unchanged as External; `UseTesseract`
default off; tessdata location per install type; English bundled; x86 out of scope.

## Reproducing

- PoC: console app, `net10.0`, `<PackageReference Include="Tesseract.CrossPlatform" Version="5.5.2.200" />`,
  `dotnet build -c Release -r win-arm64 --no-self-contained`, run with a tessdata folder and an image.
- Single-file: `dotnet publish -r win-arm64 --no-self-contained -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true`.
- MSIX: `MSBuild.exe Text-Grab-Package.wapproj -restore -p:Configuration=Release -p:Platform=ARM64
  -p:AppxBundle=Never -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxPackageSigningEnabled=false`
  with the package reference added to `Text-Grab.Core.Windows.csproj`.
