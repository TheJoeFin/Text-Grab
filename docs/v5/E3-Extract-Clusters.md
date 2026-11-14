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

After E1 and E2, `Text-Grab/Views/EditTextWindow.*.cs` partials exist per cluster, talk to `Session` rather than to controls, and their actions are registered. What remains in each partial is feature state and UI plumbing. Sizes on 2026-11-14 (re-measure): spreadsheet 2,015 lines, calc 659, files 656, Local AI 593, transcription 587, code 318, Markdown 307; the main file is 2,389 and `.Selection.cs` and `.Menus.cs` are 857 and 516.

**`calc` runs before E2**, so that panes can ship in 4.17. For that run only, the "actions are registered" part is not true yet: the calc toggle and the pane's menu items stay click handlers in the window and call into the pane. E2's `view` run converts them later.

The calc pane is already laid out as a pane: column `CalcColumn`, splitter `TextBoxSplitter`, `CalcResultsTextControl`, status border `CalcAggregateStatusBorder`, toggle `CalcToggleButton`, and persisted `CalcPaneWidth` / `HasCalcPaneOpen` (settings and `HistoryInfo`).

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
