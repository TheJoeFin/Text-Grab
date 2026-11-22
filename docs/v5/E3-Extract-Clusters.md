# E3 — Extract Edit Text Window clusters into panes and controllers

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | Large / Medium |
| Recommended model | Opus for `calc` (it sets the pattern) and `spreadsheet`; Sonnet for the rest |
| Depends on | `calc`: E1 only. Every other cluster: E2 |
| Required decisions | D4 (view-model library) — applies to new view models only |
| Unblocks | `calc`: E4a. All clusters: E4b |
| Ships in | `calc`: 4.17.0-beta2. The rest: 4.18. See [Release-Plan.md](Release-Plan.md). |
| Branch | `v5/e3-extract-<cluster>` |

Read [README.md](README.md) first.

This phase is one run per cluster. Your prompt names which. `calc` must be first. Order: `calc` → `transcription` → `localai` → `files` → `code` → `markdown` → `spreadsheet`.

## Goal

Each feature cluster that E1a put in its own partial file becomes a self-contained component: a `UserControl` (if it has UI) plus a controller class, depending only on `IEditorSession` and injected services. `EditTextWindow` shrinks to a shell that hosts them.

## Current state (verify)

After E1 and E2, `Text-Grab/Views/EditTextWindow.*.cs` partials exist per cluster, talk to `Session` rather than to controls, and their actions are registered. What remains in each partial is feature state and UI plumbing. Sizes on 2026-11-19 (`dev` @ `5e3b889`; re-measure): spreadsheet 2,018 lines, calc 659, files 667, Local AI 593, transcription 590, code 320, Markdown 596; the main file is 2,468 and `.Selection.cs` and `.Menus.cs` are 853 and 537. Four partials arrived after this plan and belong to no cluster yet: `.CommandPalette.cs` (357), `.ReadingPosition.cs` (248), `.ResizeAnchor.cs` (149) and `.Speech.cs` (74). The `calc` run leaves them in the window; assign them before the 4.18 runs.

**`calc` runs before E2**, so that panes can ship in 4.17. For that run only, the "actions are registered" part is not true yet: the calc toggle and the pane's menu items stay click handlers in the window and call into the pane. E2's `view` run converts them later.

The calc pane is already laid out as a pane: column `CalcColumn`, splitter `TextBoxSplitter`, `CalcResultsTextControl`, status border `CalcAggregateStatusBorder`, toggle `CalcToggleButton`, and persisted `CalcPaneWidth` / `HasCalcPaneOpen` (settings and `HistoryInfo`).

What ties calc to the rest of the window (checked 2026-11-19, `dev` @ `5e3b889`; line numbers drift):

- **Its state lives in the main file**, not the partial: `_debounceTimer`, `_calculationService`, `calculationResult`, `_previousTextWrapping`, `_lastCalcColumnWidth`. Restoring from history (near line 197 of `EditTextWindow.xaml.cs`), saving to history (near 542), closing (near 2220) and loading (near 2342) all touch it.
- **`selectedCultureInfo`** is shared with Toggle Case and set from `EditTextWindow.Menus.cs:321`.
- **The spreadsheet feeds it.** `GetCurrentAggregateNumbers` reads the spreadsheet selection, and `EditTextWindow.Spreadsheet.cs:742` pushes updates into the calc display. Spreadsheet is extracted in 4.18, so the pane needs a session member or event for the selected numbers.
- **It reaches into the Text editor.** It turns off `PassedTextControl.TextWrapping` while open and forwards mouse-wheel scrolling to `PassedTextControl`. The scroll-target picker near `EditTextWindow.xaml.cs:975` checks whether the pointer is over the calc results. All three must go through the surface or the session.
- **The command palette** (added after this plan) uses `CalcResultsTextControl`, `CalcCopyAllButton` and `CalcInfoButton` as window fields (`EditTextWindow.CommandPalette.cs:122`). That stops compiling once they move, so panes need a way to contribute palette entries.
- **Line-based scroll sync** can reuse `GetTopOfViewport()` in `EditTextWindow.ReadingPosition.cs`, which already finds the top visible position in each editor mode.
- **Tests:** no unit test covers calc. The UI suite uses `EditText.CalculatorToggle` and `EditText.CalculatorResults`, and `MarkdownDocumentUtilitiesTests` looks up `CalcColumn` by name (lines 376 and 474). The characterization tests start from nothing.

### After the `calc` run (2026-11-19, `v5/e3-extract-calc` @ `0fb4420`)

Done. What later runs and E4a should know, including where the run departed from the plan above:

- **`CalcPane`** (`Text-Grab/Editing/Panes/CalcPane.xaml[.cs]`) is a `UserControl` that implements `IEditorPane` itself (`View => this`). The moved code kept its code-behind style. `CalcPaneFactory` is registered in `AppServices`, but in E3 the window still declares the pane in its own XAML (`CalcPaneControl`, column 1). That XAML path is the "old calc path" E4a keeps behind its setting; the renderer makes panes through the factory.
- **Pane ↔ host.** The host sets `IsOpen` (shows the results and starts the typing delay) and binds two dependency properties: `ResultsFontSize` (to `PassedTextControl.FontSize`) and `ShowErrors` (to `ShowErrorsMenuItem.IsChecked`). The pane's Hide Pane and Show Errors context items raise `HideRequested` and `ShowErrorsToggleRequested`; the window does what those items used to do, settings included. `RecalculateAsync` and `CopyAllResults` are public for the window's toggles and for the bottom-bar button, which still resolves `CalcCopyAllButton_Click` on the window by name.
- **What stayed in the window** (`EditTextWindow.Calc.cs`, 197 lines): `CalcColumn` and its remembered width, `TextBoxSplitter` with its double-click, `CalcPaneShadow`, the open state with every toggle, history save and restore, and **wrapping**. Turning off `PassedTextControl.TextWrapping` while the pane is open stayed in `SetCalcPaneVisibility`. The window is changing its own editor, so the pane never touches it, and no session member was needed.
- **Session members added** (`IEditorSession`, Core): `Culture`; `SelectedCellNumbers` (null means no spreadsheet selection, empty means selected cells with no numbers) and `SelectedCellsChanged`; and a viewport in lines: `FirstVisibleLine`, `ViewportChanged`, `ScrollByWheel`.
- **The viewport is not on `IEditorSurface`.** It is a separate `ILineViewport`, implemented by `TextBoxSurface` and `CodeSurface`, so the Markdown and spreadsheet surfaces and the test fakes did not change. The session follows the visible editor when it is an `ILineViewport`, and otherwise the text box, which matches today's behavior in Markdown and Spreadsheet modes. The text box's line height is `FontSize × FontFamily.LineSpacing` (`TextBoxSurface.LineHeight`); a test checks this against laid-out lines.
- **Command palette:** `IEditorPane.GetPaletteCommands()` (a default interface member returning nothing) lets a pane add entries. The window loops over its `Panes`.
- **UI Automation:** `CalcPane.OnCreateAutomationPeer` returns null, so the tree under the window is the same as before the move. E4a's step-1 baseline can be captured on this code.
- **Fixed:** in Code mode the results now follow the code editor by document line, and the wheel over them scrolls the code editor.
- **Still open (for the owner):** in Code mode the code editor still wraps while calc is open (`PrepareCodeEditor` and `WrapTextCHBX_Checked` apply the Wrap menu item), so wrapped lines push results out of line below them. Collapsed folds do the same. Text mode turns wrapping off; whether Code mode should too is a decision.
- **Tests:** `CalcPaneCharacterizationTests` (written against the old code, unchanged since), `CalcPaneTests`, `EditorSessionViewportTests`. A window that is never shown does not run the layout pass that applies a scroll asked for inside a `ScrollChanged` handler, so the scroll tests' `Settle` invalidates the follower to stand in for the next frame.

One more partial, `EditTextWindow.Code.cs`, arrived with Code mode (PR #710). It is small because most of that feature was born outside the window: the control is `Text-Grab/Controls/CodeEditor*.cs` and the surface is `Text-Grab/Editing/CodeSurface.cs`. It is the nearest thing in the tree to what an extracted editor mode should look like — read it before extracting `markdown` or `spreadsheet`.

## Design

In the app, namespace `Text_Grab.Editing.Panes`:

```csharp
public sealed record PaneDescriptor(string Id /*"textgrab.pane.calc"*/, string Title, string IconKey,
    PaneDock DefaultDock /*Left|Right|Top|Bottom|Center*/, double DefaultSize, Type? ConfigType = null);

public interface IEditorPane
{
    PaneDescriptor Descriptor { get; }
    FrameworkElement View { get; }
    void Attach(IEditorSession session, JsonElement? config);
    void Detach();                       // unsubscribe, stop timers, release models
    JsonElement? SaveConfig();           // what a layout should remember (width is the host's job)
}

public interface IEditorPaneFactory     // registered in DI, one per pane type
{
    PaneDescriptor Descriptor { get; }
    IEditorPane Create();
}
```

- **AvalonDock hosts panes from E4a (D1), so design `IEditorPane` for it now.** `PaneDescriptor.Id` doubles as AvalonDock's `ContentId`. A pane's `View` is re-parented when the layout changes and when the pane is hidden or shown, which raises `Unloaded` and `Loaded` again: never tie `Attach`, `Detach` or other one-time setup to those events. Hiding is not detaching: a hidden pane stays attached and should go idle; `Detach` runs when the pane leaves the layout or the window closes.
- Clusters **with their own visual region** become panes: calc.
- Clusters that are **an editor mode** become surfaces plus a controller: Markdown, spreadsheet, code. They already implement `IEditorSurface`; this phase moves their remaining logic out of the window and into the surface's own class and control.
- Clusters that are **behavior with a little status UI** become controllers that contribute status items: transcription, Local AI, files.
- A controller is a plain class with constructor-injected services and a session. New observable state uses the D4 library. Moved code stays as it is; do not convert working code-behind to MVVM for its own sake.
- The window keeps, for now, the hand-written XAML layout. It instantiates each pane or controller and places its `View` where the old elements were. Layout from data is E4.

## Per-cluster procedure

1. Re-read the cluster's partial file. List its fields, the named XAML elements it touches, the settings it reads and writes, and what it calls on other clusters. Cross-cluster calls are the work: each becomes a session member, a registered action, or an event.
2. Add or extend tests for the cluster's observable behavior *before* moving anything.
3. Create the control and/or controller under `Text-Grab/Editing/`. Move the XAML fragment into the control's XAML unchanged, keeping every `x:Name` and `AutomationProperties.AutomationId`.
4. Move the code. Replace window field access with constructor parameters or session calls.
5. In the window, replace the removed XAML with the control and delete the partial file when empty.
6. Gates, then the manual checks for that cluster.

## Clusters

| Cluster | Becomes | Notes |
|---|---|---|
| `calc` (first, Opus) | `CalcPane : IEditorPane` | Also introduces `IEditorPane`, `PaneDescriptor`, `IEditorPaneFactory` and registers the factory. Scroll sync with the main editor (`PassedTextControl_ScrollChanged`, `SyncCalcScrollToMain`) needs a vertical scroll offset and a scrolled event on `IEditorSurface` — add the minimum, and implement it for **both** `TextBoxSurface` and `CodeSurface`. Today the calc pane does not follow the code editor at all (a known gap of Code mode); this run closes it. Line heights differ between the two editors, so sync by line, not by pixel offset. Width and open state keep persisting exactly as today. |
| `transcription` | `TranscriptionController` + status-bar control | Owns `LiveAudioTranscriber`, the gate semaphore, cancellation, the status bar. Close-while-transcribing (`StopLiveTranscriptionAndCloseAsync`) becomes a session "closing" hook. |
| `localai` | `LocalAiController` | Model lifetime and "send result to new window" settings stay where they are. |
| `files` | `DocumentFileController` | File state lives on the session already; this moves open/save/prompt logic and folder OCR. |
| `markdown` | `MarkdownEditorView` + its surface | Owns `markdownOffsetMap`, theme application, task-list checkbox handling, paste handling. |
| `code` | `CodeEditorView` + `CodeSurface` | Small. Moves `EditTextWindow.Code.cs` out of the window: the language state (`codeLanguageName`, `isCodeLanguageSet`), language detection and the language menu, the two-way selection mirror, the edit-by-edit sync into the session, and the status-bar language button (as a contributed status item). The edit sync and the selection mirror are the delicate part; `EditTextWindowCodeModeTests` covers both and must pass unchanged. |
| `spreadsheet` (last, Opus) | `SpreadsheetEditorView` + its surface | Largest. Owns the `DataTable`, undo history, cell editor tracking, column width tracking, clipboard formats. Do it in several commits: model sync → selection → editing → clipboard → context menu. |

## Out of scope

- Any new pane or feature. Any visual change.
- Rendering layout from a definition (E4). Chrome changes (E6).

## Verification

- All three test executables pass after each cluster.
- `Tests/EditTextWindow*Tests.cs` keep passing. Where a test reached into window internals that moved, retarget it to the new component; do not delete coverage.
- Manual per cluster — the feature's full flow, plus: open from history with the calc pane open restores its width; closing the window mid-transcription stops cleanly; both themes.

## Done when

All seven clusters are extracted, `EditTextWindow.xaml.cs` plus any remaining partials total under ~1,000 lines, and no component references `EditTextWindow` by type.

## Traps

- Moving XAML into a `UserControl` changes name scopes: `x:Name`s are no longer window fields, element-name bindings across the boundary break, and `x:Static` command references need their namespace. Build after every XAML move.
- Styles and resources defined in the window's `Resources` do not reach a control defined in another file unless they are in a merged dictionary. Check each moved fragment's `StaticResource` references.
- `Detach` must actually release things. Live transcription and the Whisper model hold native memory; a pane that leaks its subscription keeps the window alive.
- UI Automation IDs are a contract with the UI tests and with users' accessibility tools. Keep every one.
