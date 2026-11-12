# A1 — Action registry

| | |
|---|---|
| Track | Shared foundation (workflows and Edit Text Window both consume it) |
| Size / risk | Medium / Low to ship (inert), **high leverage** (everything later depends on its shape) |
| Recommended model | Opus |
| Depends on | F1 |
| Unblocks | W1, E2 |
| Branch | `v5/a1-action-registry` |

Read [README.md](README.md) first.

## Goal

One place in Core that describes every action Text Grab can perform on text, as data. This phase builds the registry and fills it with the pure text transforms. It changes no UI.

## Why

The same idea exists today in four unconnected shapes:

- 17 static `RoutedCommand`s and about 200 click handlers in `Text-Grab/Views/EditTextWindow.xaml.cs`.
- `Text-Grab/Models/ButtonInfo.cs` (about 100 static entries) whose `Command` and `ClickEvent` strings are bound by reflection onto `EditTextWindow` methods in `Text-Grab/Utilities/CustomBottomBarUtilities.cs`.
- `Text-Grab/Utilities/PostGrabActionManager.cs`, a second dispatcher over `ButtonInfo`.
- `Text-Grab/Utilities/TrayActions.cs` plus the hotkey router.

After v5, a transform defined once is a menu item, a ribbon button, a bottom-bar button, a post-grab action, a workflow node and a stage in the transformer pane.

## Current state (verify)

Re-verified on 2026-11-11 against `v5/e1b-editor-session` @ `a90dc0a`, after E1 split the window into partial files.

- Pure transforms already in `Text-Grab.Core/Utilities/StringMethods.cs` (1,269 lines): `TrimEachLine`, `MakeStringSingleLine`, `RemoveDuplicateLines`, `ShuffleLines`, `CorrectCommonGuidErrors`, `ToCamel`, `ReplaceReservedCharacters`, `UnstackStrings`, and more. Read the whole file and inventory it.
- Also pure and in Core: `PatternExecutor`, `RecognizerExecutor`, `TextSearchUtilities`, `ColumnSplitUtilities`, `CalculationService`.
- Still in the window, in `Text-Grab/Views/EditTextWindow.xaml.cs`: `ToggleCase` (line 1959; mutates the window's `CaseStatusOfToggle` and reads its `selectedCultureInfo`), `SplitOnSelectionCmdExecuted` and `SplitAfterSelectionCmdExecuted` (lines 1915–1957). Since E1b they read and write the document through `Session`.
- `GrabTemplateExecutor.ApplyTextOnlyTemplate` is app-side. It is **not** a pure function of `(GrabTemplate, string)`: when the template has pattern placeholders it resolves them against the user's saved patterns through `AppUtilities.TextGrabSettingsService.LoadStoredRegexes()`. Templates themselves are loaded by the app-side `GrabTemplateManager`.

## Design

Namespace `Text_Grab.Core.Actions` in `Text-Grab.Core`:

```csharp
public enum ActionKind { TextTransform, EditorCommand, AppCommand }
public enum ActionCategory { Lines, Case, Cleanup, Extract, Search, Table, Calculate, Ai, Capture, File, Window, Other }

public sealed record ActionDescriptor(
    string Id,                 // "textgrab.text.trimLines" — stable forever, never renamed
    string DisplayName,
    string IconKey,            // a plain string; the app maps it to a Wpf.Ui SymbolRegular
    ActionCategory Category,
    ActionKind Kind,
    Type? ConfigType = null,   // a record describing user-settable parameters, or null
    string? DefaultGesture = null,
    bool RequiresCopilotPlus = false);

public interface ITextTransform
{
    ActionDescriptor Descriptor { get; }
    ValueTask<string> ApplyAsync(string input, JsonElement? config, CancellationToken ct);
}

public interface IActionRegistry
{
    IReadOnlyCollection<ActionDescriptor> All { get; }
    bool TryGet(string id, out ActionDescriptor descriptor);
    ITextTransform? GetTransform(string id);
    void Register(ITextTransform transform);
    void Register(ActionDescriptor descriptor);   // for kinds whose implementation lives elsewhere
}
```

- A `DelegateTextTransform` helper wraps a `Func<string, string>` so a pure `StringMethods` function registers in one line.
- ID scheme: `textgrab.<area>.<verb>`, lower camel case after the second dot. IDs are persisted in users' layouts, workflows and button settings. Once shipped they are never changed; add a test that pins the full ID list so a rename fails the build.
- `IEditorCommand` is declared in E2, because it needs `IEditorSession` from E1. The registry accepts its descriptor now and its implementation later.
- Registration: a static `CoreActions.RegisterAll(IActionRegistry)` in Core. The app's composition root creates the registry singleton and calls it. Core.Windows adds its own `RegisterAll` for Windows-backed transforms.

## Steps

1. **Inventory.** Produce a table of every candidate: source method, proposed ID, display name (copy the exact menu text from `EditTextWindow.xaml`), category, whether it needs config. Save it as an appendix at the bottom of this file. Mark anything that needs selection, caret or window state as "E2, not A1".
2. Registry types + `ActionRegistry` implementation + tests in `Tests.Core` (register, duplicate ID throws, lookup, pinned ID list).
3. Register the pure `StringMethods` transforms through `DelegateTextTransform`. One test per transform asserting it calls through to the same result as the direct method.
4. **Lift `ToggleCase`.** Characterization tests first, driving the existing window behavior through its current tests or a new one. Then extract a pure `(string text, CurrentCase state, CultureInfo culture) → (string result, CurrentCase next)` into Core. The window keeps `CaseStatusOfToggle` and calls the pure function.
5. **Lift split-on-selection / split-after-selection** to pure `(string fullText, string selection) → string` in Core, the same way. These two need a selection, so they are *not* registered as `ITextTransform`; they are helpers E2 will wrap.
6. Move `ApplyTextOnlyTemplate` to Core if it moves without dragging settings or WPF with it; register a parameterized "apply template" transform whose config carries the template ID only if a headless way to load the template exists. If not, note it and move on.
7. Register transforms with config for: unstack (column count), pattern extraction (pattern ID or regex), recognizer extraction.
8. In Core.Windows, register descriptors (and transforms where the service is headless) for translate, summarize, rewrite and text-to-table with `RequiresCopilotPlus = true`. Read `WinAiTranslator`, `WinAiMeetingNotes`, `WinAiLanguageModel` to see what is callable without a window.
9. Register the registry in `AppServices`. Nothing in the UI uses it yet.

## Out of scope

- Any change to menus, the bottom bar, `ButtonInfo`, post-grab actions or hotkeys (E2).
- `IEditorCommand` and `IEditorSession`.
- Workflow steps (W1).

## Verification

All three test executables pass. `TierBoundaryTests` passes. The existing `EditTextWindow*Tests` pass unchanged — proof the two lifts did not change behavior.

## Done when

- The registry resolves from DI and lists every pure transform with a pinned, tested ID.
- `ToggleCase` and both split functions are pure functions in Core that the window calls.
- The inventory appendix exists and marks what E2 must convert.

## Traps

- `IconKey` must stay a string. Do not reference `Wpf.Ui` from Core; `TierBoundaryTests` will fail.
- Some `StringMethods` functions are culture-sensitive. `ITextTransform.ApplyAsync` has no culture parameter: decide once (current culture, as today) and write it in the interface's XML doc.
- Do not "improve" a transform while wrapping it. If one looks buggy, note it in the report.
- Display names are user-facing strings users already know. Copy them; do not rewrite them.

## A1 outcome (2026-11-11, branch `v5/a1-action-registry`, last code commit 96e59be)

Branched from `v5/e1b-editor-session` @ `a90dc0a`, not `dev`. E1 is not merged into `dev` yet, and steps 4–5 edit code that E1 moved into new places.

**What was built, and how it differs from the sketch:**

- `Text_Grab.Core.Actions` in Core: `ActionKind`, `ActionCategory`, `ActionDescriptor` and `ITextTransform` as designed; `IActionRegistry` as designed, with `[MaybeNullWhen(false)]` on `TryGet`. `ActionRegistry` is thread-safe and lists actions in registration order. It compares IDs ordinally, rejects IDs that do not match `textgrab.<area>.<verb>` (three parts, lower camel case), rejects blank display names, and throws `ArgumentException` on a duplicate ID.
- `ITextTransform`'s remarks set the contract: current culture; JSON config read case-insensitively; failure by exception (`ArgumentException` for bad config, `InvalidOperationException` with a user-facing message when the transform cannot produce a result, `OperationCanceledException`), never error text returned as a result.
- `DelegateTextTransform` (no config) and `DelegateTextTransform<TConfig>` (config record). An internal `ActionConfig` reader applies the same rules to every action: property names in any case, enums by name or number, unknown properties ignored, and a missing or null required value or a wrong JSON type is an `ArgumentException`.
- `CoreActions.RegisterAll` registers 13 transforms and `WindowsActions.RegisterAll` (Core.Windows) registers 6 AI actions; see appendix A. `AppServices` registers one `IActionRegistry` filled from both.
- Clean Up Text is registered with the config transforms (step 7), not in step 3, because it takes `CorrectToLatin`.
- `DefaultGesture` is set only on Replace Reserved Characters (`Ctrl+R`): its gesture runs the same handler as the menu item. Unstack's `Ctrl+U` belongs to E2's selection-driven command.
- Step 6: `ApplyTextOnlyTemplate` did not move. It resolves pattern placeholders through the app's settings service, and templates load through the app-side `GrabTemplateManager`, so there is no headless way to load a template. No apply-template transform is registered.
- Step 7: pattern extraction takes a regex, not a saved-pattern ID, for the same reason: saved patterns only load app-side. A `PatternId` can be added to `ApplyPatternConfig` later without breaking stored config.
- Step 8: Rewrite and Convert to Table are descriptors only, because their services return `"ERROR: …"` as the text. Meeting notes is registered too (the plan points at `WinAiMeetingNotes`, and it is an Edit Text Window menu item).
- New display names: "Apply Pattern" and "Apply Smart Pattern" (no existing string fits). The config-driven unstack transforms keep the menu's "(Select Top Row)" / "(Select First Column)" text, which reads oddly outside the window. Display names are not persisted, so either can change cheaply.

**Found while wrapping, not changed:**

- Toggle Case's first press re-applies the case it detects, so all-lower or all-upper text does not visibly change until the second press. Title case leaves an all-capitals word unchanged, and in Spreadsheet mode toggle case never changes an all-lower or all-upper cell.
- Rewrite and Convert to Table put `"ERROR: …"` into the document when Windows AI fails.
- `PatternExecutor` returns an empty string for an invalid regex, so Apply Pattern with a bad regex produces empty output rather than an error.
- `WindowsAiUtilities.SummarizeParagraph` takes no cancellation token.

**Tests:** Tests.Core: `ActionRegistryTests`, `CoreActionsTests` (pinned IDs), `DelegateTextTransformTests`, `CoreTextTransformTests` and `CoreConfigTransformTests` (each transform against the function it wraps), `ToggleCaseTests`, `SplitOnSelectionTests`. Tests.Core.Windows: `WindowsActionsTests` (pinned IDs; the unavailable-device paths). Tests: `EditTextWindowToggleCaseTests` (9) and `EditTextWindowSplitOnSelectionTests` (7), written and passing against the old code first; a registry case in `AppServicesTests`; `ActionIconKeyTests`.

## Appendix — action inventory

Built in step 1 from `EditTextWindow.xaml` (menus and the selection context menu), `ButtonInfo.AllButtons`, `PostGrabActionManager`, `StringMethods` and the Windows AI utilities.

Conventions:

- **Display name** is the visible text of the `EditTextWindow.xaml` menu item, with access-key underscores removed. Where no menu item exists the `ButtonInfo` text is used. Names marked **new** have no existing string and were written for the registry.
- **Icon** is the `Wpf.Ui` `SymbolRegular` member name the matching `ButtonInfo` uses.
- **A1** rows are registered in this phase; their IDs are pinned by tests and never change.
- **E2** rows need selection, caret, mode, file or window state, or open UI. They are not registered here. Their IDs are suggestions for E2; nothing pins them until E2 registers them.

### A. Registered in A1

Text transforms in `Text-Grab.Core` (`CoreActions.RegisterAll`):

| ID | Source | Display name | Category | Config | Icon | Notes |
|---|---|---|---|---|---|---|
| `textgrab.text.trimLines` | `StringMethods.TrimEachLine` | Trim Each Line | Lines | — | `TextCollapse24` | The window applies it to all text in Text mode and per cell in Spreadsheet mode. Post-grab action `TrimEachLine_Click`. |
| `textgrab.text.makeSingleLine` | `StringMethods.MakeStringSingleLine` | Make Single Line | Lines | — | `SubtractSquare24` | `SingleLineCmd` |
| `textgrab.text.removeDuplicateLines` | `StringMethods.RemoveDuplicateLines` | Remove Duplicate Lines | Lines | — | `MultiselectLtr24` | The window applies it to all text. Post-grab action `RemoveDuplicateLines_Click`. |
| `textgrab.text.shuffleLines` | `StringMethods.ShuffleLines` | Shuffle Lines | Lines | — | `ArrowShuffle24` | Random order from `Random.Shared`. |
| `textgrab.text.cleanUp` | `StringMethods.CleanUpText` | Clean Up Text | Cleanup | `CleanUpTextConfig(CorrectToLatin = false)` | `TextClearFormatting24` | The window and the post-grab action pass `AppUtilities.ShouldCorrectToLatin()`; E2 passes it as config. |
| `textgrab.text.tryMakeNumbers` | `StringMethods.TryFixToNumbers` | Try To Make Numbers | Cleanup | — | `NumberRow24` | |
| `textgrab.text.tryMakeLetters` | `StringMethods.TryFixToLetters` | Try To Make Letters | Cleanup | — | `TextT24` | |
| `textgrab.text.correctGuidErrors` | `StringMethods.CorrectCommonGuidErrors` | Correct Common GUID/UUID Errors | Cleanup | — | `TextWholeWord20` | Post-grab action `CorrectGuid_Click` ("Fix GUIDs"). |
| `textgrab.text.replaceReservedCharacters` | `StringMethods.ReplaceReservedCharacters` | Replace Reserved Characters | Cleanup | — | `RoadCone24` | `ReplaceReservedCmd`; default gesture `Ctrl+R`. |
| `textgrab.text.unstack` | `StringMethods.UnstackStrings` | Unstack Text (Select Top Row) | Table | `UnstackConfig(Columns)` | `TableStackAbove24` | The window derives `Columns` from the selection (E2 row below). |
| `textgrab.text.unstackGroups` | `StringMethods.UnstackGroups` | Unstack Text (Select First Column) | Table | `UnstackGroupsConfig(Rows)` | `TableStackLeft24` | The window derives `Rows` from the selection (E2 row below). |
| `textgrab.text.applyPattern` | `PatternExecutor.Apply` over a regex | Apply Pattern (**new**) | Extract | `ApplyPatternConfig(Regex, Mode = "all", Separator = ", ")` | `TextEffects24` | Takes the regex text. Looking up a saved pattern by ID needs the user's saved patterns, which only the app can load today. |
| `textgrab.text.applyRecognizer` | `RecognizerExecutor.ApplyRecognizer` | Apply Smart Pattern (**new**) | Extract | `ApplyRecognizerConfig(RecognizerId, Mode = "all", Separator = ", ", Output = ResolvedValue)` | `DocumentSearch24` | `RecognizerId` is a `BuiltInRecognizer.Id` such as `datetime`. |

Windows AI actions in `Text-Grab.Core.Windows` (`WindowsActions.RegisterAll`), all `RequiresCopilotPlus = true`, category Ai:

| ID | Source | Display name | Config | Icon | Implementation |
|---|---|---|---|---|---|
| `textgrab.ai.summarize` | `WindowsAiUtilities.SummarizeParagraph` | Summarize Paragraph | — | `BotSparkle24` | Transform |
| `textgrab.ai.meetingNotes` | `WinAiMeetingNotes.SummarizeAsync` | Summarize as Meeting Notes | — | `NotepadPerson20` | Transform |
| `textgrab.ai.rewrite` | `WindowsAiUtilities.Rewrite` | Rewrite | — | `BotSparkle24` | Descriptor only: the service returns failures as `"ERROR: …"` text, which a transform cannot tell apart from a real result. |
| `textgrab.ai.convertToTable` | `WindowsAiUtilities.TextToTable` | Convert to Table | — | `BotSparkle24` | Descriptor only, for the same reason. |
| `textgrab.ai.translate` | `WinAiTranslator.TranslateAsync` | Translate to... | `TranslateConfig(TargetLanguage)` | `Translate24` | Transform. `TargetLanguage` is the English language name the menu items carry in `Tag` ("Spanish"). |
| `textgrab.ai.translateToSystemLanguage` | `WinAiTranslator.TranslateAsync` with `LanguageUtilities.GetSystemLanguageForTranslation()` | Translate to System Language | — | `Translate24` | Transform. Also the post-grab `Translate_Click` case. |

### B. Lifted into Core in A1, wrapped by E2

| Pure function (A1) | Window caller | Why it is not a transform |
|---|---|---|
| `StringMethods.ToggleCase(text, state, culture) → (result, next)` | `ToggleCase` (`ToggleCaseCmd`, Shift+F3) | The next case depends on the window's `CaseStatusOfToggle`, and the culture is the window's language. |
| `StringMethods.SplitOnSelection(fullText, selection)` | `SplitOnSelectionCmdExecuted` | Needs the selection. |
| `StringMethods.SplitAfterSelection(fullText, selection)` | `SplitAfterSelectionCmdExecuted` | Needs the selection. |

### C. E2, not A1

Grouped by E2 cluster. "Core" names the pure function an E2 command can wrap, where one exists.

**Lines / cleanup (foundation run)**

| Source | Display name | Suggested ID | Needs | Core |
|---|---|---|---|---|
| `ToggleCase` (`ToggleCaseCmd`) | Toggle Case | `textgrab.text.toggleCase` | `CaseStatusOfToggle`, window culture; Shift+F3 | `StringMethods.ToggleCase` |
| `UnstackExecuted` (`UnstackCmd`) | Unstack Text (Select Top Row) | `textgrab.selection.unstack` | selection line count → `textgrab.text.unstack` config; Ctrl+U | `UnstackStrings` |
| `UnstackGroupExecuted` (`UnstackGroupCmd`) | Unstack Text (Select First Column) | `textgrab.selection.unstackGroups` | selection line count → `textgrab.text.unstackGroups` config | `UnstackGroups` |
| `JoinLinesMenuItem_Click` | Join Lines... | `textgrab.window.joinLines` | opens `JoinLinesWindow` | `JoinLines` (four parameters; not registered) |
| `AddRemoveAtMenuItem_Click` | Add, Remove, Limit... | `textgrab.window.addRemoveLimit` | opens `AddOrRemoveWindow` | `AddCharsToEachLine`, `RemoveFromEachLine`, `LimitCharactersPerLine` (not registered) |

**Selection**

| Source | Display name | Suggested ID | Needs | Core |
|---|---|---|---|---|
| `SplitOnSelectionCmdExecuted` | Split Lines Before Each Selection | `textgrab.selection.splitBefore` | selection | `StringMethods.SplitOnSelection` |
| `SplitAfterSelectionCmdExecuted` | Split Lines After Each Selection | `textgrab.selection.splitAfter` | selection | `StringMethods.SplitAfterSelection` |
| `IsolateSelectionCmdExecuted` | Isolate Selected Text | `textgrab.selection.isolate` | selection; Ctrl+I | — |
| `DeleteAllSelectionExecuted` | Delete All Instances of Selection | `textgrab.selection.deleteAll` | selection | `RemoveAllInstancesOf` |
| `DeleteAllSelectionPatternExecuted` | Delete All Instances of Selection Simple Pattern | `textgrab.selection.deleteAllPattern` | selection | `ExtractSimplePattern` |
| `InsertSelectionOnEveryLine` | Insert Selection On Every Line | `textgrab.selection.insertOnEveryLine` | selection and caret column | — |
| `SelectWordMenuItem_Click` | Select Word | `textgrab.selection.selectWord` | caret; Ctrl+W | `CursorWordBoundaries` |
| `SelectLineMenuItem_Click` | Select Line | `textgrab.selection.selectLine` | caret; Ctrl+L | `GetStartAndLengthOfLineAtPosition` |
| `SelectAllMenuItem_Click` | Select All | `textgrab.selection.selectAll` | editor; Ctrl+A | — |
| `SelectNoneMenuItem_Click` | Select None | `textgrab.selection.selectNone` | editor | — |
| `DeleteSelectedTextMenuItem_Click` | Delete Selected Text | `textgrab.selection.delete` | selection | — |
| `MoveLineUpMenuItem_Click` | Move Selection Up | `textgrab.selection.moveUp` | selection; Alt+Up | — |
| `MoveLineDownMenuItem_Click` | Move Selection Down | `textgrab.selection.moveDown` | selection; Alt+Down | — |
| gesture-only commands in `SetupRoutedCommands` | (duplicate line, add line above) | `textgrab.selection.duplicateLine`, `textgrab.selection.addLineAbove` | caret; Ctrl+D, Ctrl+Enter | — |

**Search**

| Source | Display name | Suggested ID | Needs |
|---|---|---|---|
| `DefaultWebSearchExecuted` | Default Web Search | `textgrab.search.webDefault` | selection, launches browser |
| `WebSearchCollection` items (`WebSearchExecuted`) | Search web with... | `textgrab.search.web` (searcher as config) | selection, the menu item's searcher |
| `FindAndReplaceMenuItem_Click` | Find and Replace | `textgrab.search.findAndReplace` | opens window; Ctrl+Shift+F |
| `SimilarMatchesButton_Click` | Find Similar Matches | `textgrab.search.similarMatches` | selection, opens window |
| `RegexPatternButton_Click` | Open Regex Pattern Search | `textgrab.search.regexPattern` | selection, opens window |
| `SavePatternMenuItem_Click` | Save Regex Pattern | `textgrab.search.savePattern` | selection, opens dialog |
| `CharDetailsButton_Click` | Show Character Details | `textgrab.search.charDetails` | caret, opens UI |
| `MakeQrCodeExecuted` | Make QR Code | `textgrab.search.qrCode` | opens window |
| `LaunchUriExecuted` | Launch URL | `textgrab.search.launchUrl` | selection or word at caret, launches browser |

**Templates**

| Source | Display name | Suggested ID | Needs |
|---|---|---|---|
| `ApplyPatternItem_Click` | Patterns (each saved pattern) / Smart Patterns (each recognizer) | `textgrab.text.applyPattern` / `textgrab.text.applyRecognizer` with config from the item's `PatternItem` | the menu item's `Tag` |
| `ApplyPatternPerLineItem_Click` | Apply Per Line (Template or Pattern) › Patterns / Smart Patterns | E2 decides: a per-line flag in config, or own IDs | the menu item's `Tag` |
| `ApplyGrabTemplateItem_Click` | Grab Templates (each template) | `textgrab.text.applyTemplate` | templates load app-side; see step 6 |
| `ApplyGrabTemplatePerLineItem_Click` | Apply Per Line (Template or Pattern) › Grab Templates | as above | as above |

**Files and window**

Open File... (Ctrl+O), Recent, Recent Grabs, Recent Files, Save (Ctrl+S), Save As... (Ctrl+Shift+S), Copy And Close, Close and Insert, Text Grab Settings..., Close, New Window, New Window from Selection (Ctrl+N), Open Recent Edit Window (Ctrl+Shift+T), List Files and Folders From Folder..., Extract Text from Images in Folder... and its four folder-OCR toggles, Edit Bottom Bar. All need file or window state or open UI. Suggested area: `textgrab.file.*` and `textgrab.window.*`.

**Capture**

New Fullscreen Grab (Ctrl+F), Fullscreen with 2 second delay, Grab previous region, New Grab Frame (Ctrl+G), Capture from Camera..., OCR Paste (Ctrl+Shift+V, cached clipboard state), Add From Quick Simple Lookup (Ctrl+Q), Watch Clipboard For Changes, OCR in Language..., Use Grab Template..., Manage Grab Templates..., Open Patterns Manager..., Edit Last Grab. Suggested area: `textgrab.capture.*`.

**AI**

The A1 descriptors above, plus: Extract RegEx (opens a dialog with the pattern and its explanation), Restart Local LLM, Learn About Local AI Features..., and the toggles Send Result to New Window and Notify When Done. The 12 per-language bottom-bar buttons (`TranslateToSpanish_Click` and the rest) map to `textgrab.ai.translate` with config, or to their own IDs; E2 decides. All need window state for where the result goes (in place, per cell, or a new window).

**Transcription, spreadsheet, view**

Transcribe Audio, Transcription Options and its sources and models, Open Audio / Video...; every spreadsheet menu and context-menu item, including Transpose Table (it edits the spreadsheet document and moves focus); Raw Text / Spreadsheet / Markdown modes, Show Calc Errors, Use Margins, Wrap Text, Font..., Spell Check modes, Always On Top, Hide Bottom Bar, Calculation Pane (Ctrl+P), Copy All Calculation Results, Restore This Window Position. All need window state.

**Post-grab actions** (`PostGrabActionManager.ExecutePostGrabAction`)

| `ClickEvent` | Maps to |
|---|---|
| `CorrectGuid_Click` | `textgrab.text.correctGuidErrors` |
| `TrimEachLine_Click` | `textgrab.text.trimLines` |
| `CleanUpText_Click` | `textgrab.text.cleanUp` with `CorrectToLatin` from `AppUtilities.ShouldCorrectToLatin()` |
| `RemoveDuplicateLines_Click` | `textgrab.text.removeDuplicateLines` |
| `Translate_Click` | `textgrab.ai.translateToSystemLanguage` (the case is still handled; its default-list entry is commented out) |
| `WebSearch_Click`, `Insert_Click`, `SpeakText_Click` | app commands (browser, insert after close, TTS) |
| `ApplyTemplate_Click` | needs the capture region to re-OCR; not a text transform |

### D. Pure functions not registered

| Function | Why |
|---|---|
| `ToCamel` | No caller and no menu item. |
| `TryFixEveryWordLetterNumberErrors`, `TryFixNumberLetterErrors` | OCR post-processing (settings-driven), not a user action. |
| `ReplaceGreekOrCyrillicWithLatin` | OCR post-processing, and part of `CleanUpText` through `CorrectToLatin`. |
| `JoinLines`, `AddCharsToEachLine`, `RemoveFromEachLine`, `LimitCharactersPerLine` | Reached only through dialogs. Each needs a config schema, and "Add, Remove, Limit..." is one menu item for three operations, so there is no display name to copy. Register them when a consumer needs them. |
| `RemoveNonWordChars`, `ExtractSimplePattern`, `EscapeSpecialRegexChars`, `ExplainRegexPattern`, word, line and count helpers | Helpers for other features, not text-to-text actions. |
| `EditTextTableDocument.Transpose`, `ColumnSplitUtilities`, `CalculationService`, `TextSearchUtilities` | Back spreadsheet, calc-pane and search features that need window state. |
| `GrabTemplateExecutor.ApplyTextOnlyTemplate` | App-side and reads settings; see step 6. |
