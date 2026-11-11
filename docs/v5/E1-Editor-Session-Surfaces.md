# E1 — Editor session and surfaces

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | Large / **High** (selection, undo and sync semantics across three editors) |
| Recommended model | **E1a: Sonnet. E1b: Opus.** |
| Depends on | — |
| Unblocks | E2 |
| Branches | `v5/e1a-etw-partials`, then `v5/e1b-editor-session` |

Read [README.md](README.md) first.

This phase has two parts, each its own branch and pull request. **Do only the part named in your prompt.**

## Goal

Stop everything — inside and outside the window — from reaching directly into the Edit Text Window's controls. After this phase, code talks to an `IEditorSession`; only one small class per editor mode knows which WPF control is behind it.

## Current state (verify)

- `Text-Grab/Views/EditTextWindow.xaml.cs`: one class, 8,206 lines before E1a (`dev` @ `b8adc26`). `EditTextWindow.xaml`: 1,538 lines, 205 `MenuItem`s. After E1a the class is split across nine files; see "E1a outcome".
- Three editors switched by `EtwEditorMode` (`Text`, `Markdown`, `Spreadsheet`; enum in `Text-Grab.Core/Models/EditTextTableDocument.cs`): `PassedTextControl` (`TextBox`), `MarkdownEditorControl` (`RichTextBox`), `SpreadsheetDataGrid` (`DataGrid`).
- `PassedTextControl.Text` is the source of truth; the other two editors sync to and from it through `SyncTextFromActiveEditor`, `SyncActiveEditorFromText`, `RefreshSpreadsheetFromText`, `RefreshMarkdownFromText`, `SyncMarkdownTextFromDocument`, guarded by flags `isSyncingTextFromSpreadsheet`, `isSyncingTextFromMarkdown`, `isApplyingSpreadsheetLayout`, `isApplyingMarkdownDocument`.
- `PassedTextControl` is referenced 280 times inside the window and 20 times from other files. `EditTextWindow` is referenced from 29 other app files, among them `FindAndReplaceWindow`, `JoinLinesWindow`, `AddOrRemoveWindow`, `SplitColumnWindow`, `RegexManager`, `BottomBarSettings`, `OutputUtilities`, `HistoryService`, `NotificationUtilities`, `CustomBottomBarUtilities`, `PostGrabActionManager`, `GrabFrame`, `FullscreenGrab`, `QuickSimpleLookup`.
- Existing helper methods that already hide the mode: `GetSelectedTextOrAllText`, `ReplaceSelectedTextOrAllText`, `ApplySelectedTextOrAllTextTransform(Func<string,string>)`, `ApplySelectedTextOrAllTextTransformAsync`, `SelectInEditor`, `GetSpreadsheetAwareSelectedText`, `TryApplySpreadsheetTextTransform`.
- Precedent for partial files: `FullscreenGrab.xaml.cs`, `FullscreenGrab.SelectionStyles.cs`, `FullscreenGrab.FindOnScreen.cs`.
- Tests: seven `Tests/EditTextWindow*Tests.cs` suites, `FindAndReplaceWindowTests`, `GrabFrameEtwTests`, the Markdown suites.

---

## E1a — Split the code-behind into partial files (Sonnet)

**A pure file reorganization. Zero behavior change, zero signature change, zero logic edits.**

Target files, all `public partial class EditTextWindow`, all in `Text-Grab/Views/`:

| File | Contents |
|---|---|
| `EditTextWindow.xaml.cs` | Fields, constructors, properties, window lifecycle (`Window_Loaded`, `Window_Closing`, …), `SetupRoutedCommands`, anything not listed below |
| `EditTextWindow.Spreadsheet.cs` | Every method with `Spreadsheet` in its name, plus the nested `SpreadsheetCellTextWrappingConverter` |
| `EditTextWindow.Markdown.cs` | `*Markdown*` methods and `MarkdownEditorControl_*` handlers |
| `EditTextWindow.Calc.cs` | Calc pane: `InitializeExpressionEvaluator`, `EvaluateExpressions`, `*Calc*`, `*Aggregate*`, the nested `AggregateType` |
| `EditTextWindow.Transcription.cs` | Audio file and live transcription: `TranscribeAudioFilesAsync`, `*Transcription*`, `LiveTranscri*`, `*LiveSource*`, `*TranscriptionModel*` |
| `EditTextWindow.LocalAi.cs` | `Summarize*`, `MeetingNotes*`, `*LocalAi*`, `Rewrite*`, `ConvertTable*`, `Translate*`, `PerformTranslationAsync`, `ExtractRegexMenuItem_Click` |
| `EditTextWindow.Files.cs` | Open/save/file state: `OpenPath`, `Save*`, `SetOpenedFileState`, `UpdateWindowTitle`, `*PendingFile*`, `GetDefaultSaveExtension`, `GetSaveDocumentFilterIndex`, drag and drop, folder OCR (`OcrAllImagesInFolder`, `OcrAllImagesInParallel`, `ReadFolderOfImages_Click`) |
| `EditTextWindow.Menus.cs` | Dynamic menu population: `Load*MenuItems`, `Clear*MenuItems`, `Populate*Menu`, `*_SubmenuOpened`, recent-items menus |
| `EditTextWindow.Selection.cs` | Selection, bottom-bar status and pattern buttons: `UpdateLineAndColumnText`, `UpdateSelectionSpecificUI`, `MatchCountButton_Click`, `SimilarMatchesButton_Click`, `RegexPatternButton_Click`, `CharDetailsButton_Click`, `Select*` |

Steps:

1. Build and run all tests to get a baseline count.
2. Move one cluster per commit. Cut and paste whole members only; keep fields in the main file. Each new file gets only the `using`s it needs.
3. After each commit: primary build gate, then `Tests.exe`.
4. Members that fit two clusters go to the one they call into most; do not agonize.

Done when: the main file is under ~2,500 lines, the test count and results equal the baseline, and `git diff --stat` shows only moves (verify with `git diff -M --stat` and by confirming the total line count across the partials equals the original plus the added file headers).

Traps: the XAML names its handlers; renaming a method breaks the XAML compile but a *deleted* duplicate does not. `#region` blocks span the whole file — remove the regions rather than leaving them unbalanced.

### E1a outcome (2026-11-11, branch `v5/e1a-etw-partials`, last move 21428ec)

| File | Lines |
|---|---|
| `EditTextWindow.xaml.cs` | 2,367 |
| `EditTextWindow.Spreadsheet.cs` | 1,997 |
| `EditTextWindow.Selection.cs` | 851 |
| `EditTextWindow.Calc.cs` | 659 |
| `EditTextWindow.Files.cs` | 637 |
| `EditTextWindow.LocalAi.cs` | 593 |
| `EditTextWindow.Transcription.cs` | 586 |
| `EditTextWindow.Menus.cs` | 506 |
| `EditTextWindow.Markdown.cs` | 315 |

- The name patterns above left the main file at ~3,150 lines, so members that only one cluster calls, or that clearly belong to it, went with that cluster. Examples: the calc-only handlers (`DebounceTimer_Tick`, `ShowErrors*`, `TextBoxSplitter_MouseDoubleClick`, `CopyAllContextItem_Click`); the pattern-button and bottom-bar code (to Selection); the handlers attached by the dynamic menu builders and the `AddPossible*RightClickMenu` builders (to Menus). Each commit message lists its extras.
- `SavePatternMenuItem_Click` and `SaveCurrentPatternToRegexManager` match `Save*` but are pattern-button code, so they are in Selection, not Files.
- `ClearGrabTemplateMenuItems` and `ClearDynamicMenuItems` stay in the main file. `EditTextWindowGrabTemplateMenuTests.ClearGrabTemplateMenuItems_RestoresPlaceholderAfterClearing` reads `EditTextWindow.xaml.cs` as text and expects both there, in that order.
- The four `#region`s (Fields, Constructors, Properties, Methods) stay in the main file and stay balanced. They were not removed.
- `PassedTextControl_*` handlers stay in the main file with the text editor they belong to.

---

## E1b — Introduce the session and surfaces (Opus)

Requires E1a merged.

### Design

In `Text-Grab.Core`, namespace `Text_Grab.Core.Editing` (pure; no WPF):

```csharp
public readonly record struct TextRange(int Start, int Length);

public interface IEditorSession
{
    string Text { get; set; }                 // whole document as plain text
    EtwEditorMode Mode { get; }
    TextRange Selection { get; set; }         // in plain-text offsets
    string SelectedText { get; }
    string GetSelectedTextOrAllText();
    void ReplaceSelectedTextOrAllText(string replacement);
    void ReplaceRange(TextRange range, string replacement);
    void InsertAtCaret(string text);
    void Select(TextRange range, bool scrollIntoView = true);

    string? FilePath { get; }
    bool HasPendingEdits { get; }

    event EventHandler TextChanged;
    event EventHandler SelectionChanged;
    event EventHandler ModeChanged;
}
```

Adjust the member list to what callers actually need — derive it from the census in step 1, not from this sketch. Keep it small.

In the app, namespace `Text_Grab.Editing`:

```csharp
internal interface IEditorSurface        // one per editor mode
{
    EtwEditorMode Mode { get; }
    string GetText();
    void SetText(string text);
    TextRange GetSelection();
    void SetSelection(TextRange range, bool scrollIntoView);
    void ReplaceRange(TextRange range, string replacement);
    void Focus();
    event EventHandler TextChanged;
    event EventHandler SelectionChanged;
}
```

`TextBoxSurface`, `MarkdownSurface`, `SpreadsheetSurface` wrap the three controls. `EditorSession` (app) implements `IEditorSession`, owns the active surface and the file state, and is exposed as `EditTextWindow.Session`.

### Steps

1. **Census.** For all 280 internal and 20 external `PassedTextControl` uses, classify each: read text, write text, read selection, write selection, replace selection, caret/scroll, event subscription, or control-specific (font, wrap, spell check, context menu, focus). Put the totals per category in your report. Control-specific uses stay as they are; they belong to the surface, not the session.
2. **Characterization tests** for the cross-editor behaviors that have none today: a transform applied to a selection in each mode; undo after a transform in each mode; switching modes preserves text; find-and-select in each mode. Pass on the old code first.
3. Add `TextRange` and `IEditorSession` to Core; `IEditorSurface`, the three surfaces and `EditorSession` to the app. The surfaces initially *delegate to the window's existing methods* — move no logic yet.
4. **External callers first** (20 uses, 29 files): change them to use `window.Session`. `FindAndReplaceWindow` is the most coupled; do it first and keep `FindAndReplaceWindowTests` green.
5. **Internal callers** by partial file, smallest first. One commit per file.
6. Only then move the sync logic (`SyncTextFromActiveEditor` and friends) from the window into `EditorSession`, with the `isSyncing*` flags. This is the riskiest commit; keep it separate.
7. Make `PassedTextControl`, `MarkdownEditorControl` and `SpreadsheetDataGrid` non-public to other classes where the compiler allows (`x:FieldModifier="private"`), so the boundary is enforced.

### Out of scope

- Converting commands or menus (E2). Extracting panes (E3). Any visual change.
- Changing which control is the source of truth for text.

### Verification

- All three test executables pass; every `EditTextWindow*Tests`, `FindAndReplaceWindowTests`, `GrabFrameEtwTests` and Markdown suite passes.
- Manual, in each of the three modes: type, select, apply three different transforms, undo, redo, find and replace, switch mode and back, open a file, save, close with unsaved edits. Also: a fullscreen grab into an open Edit Text Window; Grab Frame sending text to it; the clipboard watcher; live transcription appending text.

### Done when

No file outside `Text-Grab/Views/EditTextWindow*.cs` and `Text-Grab/Editing/` references `PassedTextControl`, `MarkdownEditorControl` or `SpreadsheetDataGrid`.

### Traps

- Offsets: the `TextBox` uses plain-text offsets with `\r\n`; the `RichTextBox` uses `TextPointer`s and the window keeps a `markdownOffsetMap` to translate; the spreadsheet has cells, not offsets. `TextRange` is always plain-text offsets into `Session.Text`. Each surface translates.
- Programmatic `TextBox.Text = …` clears the undo stack; the existing code sometimes uses `SelectedText = …` to keep it. Preserve which one each call site used.
- Re-entrancy: a surface raising `TextChanged` while the session is pushing text into it. Keep the existing guard flags; do not replace them with something cleverer in this phase.
- `HistoryService` and `NotificationUtilities` construct or reactivate windows by type. They keep referencing `EditTextWindow`; only their reach into its *controls* changes.
