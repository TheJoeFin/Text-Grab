# E3 — Extract Edit Text Window clusters into panes and controllers

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | Large / Medium |
| Recommended model | Opus for `calc` (it sets the pattern) and `spreadsheet`; Sonnet for the rest |
| Depends on | E2 |
| Required decisions | D4 (view-model library) — applies to new view models only |
| Unblocks | E4 |
| Branch | `v5/e3-extract-<cluster>` |

Read [README.md](README.md) first.

This phase is one run per cluster. Your prompt names which. `calc` must be first.

## Goal

Each feature cluster that E1a put in its own partial file becomes a self-contained component: a `UserControl` (if it has UI) plus a controller class, depending only on `IEditorSession` and injected services. `EditTextWindow` shrinks to a shell that hosts them.

## Current state (verify)

After E1 and E2, `Text-Grab/Views/EditTextWindow.*.cs` partials exist per cluster, talk to `Session` rather than to controls, and their actions are registered. What remains in each partial is feature state and UI plumbing. Approximate sizes before E1 (re-measure): spreadsheet ≈ 2,300 lines, calc ≈ 700, transcription ≈ 550, Local AI ≈ 450, files ≈ 400, Markdown ≈ 300.

The calc pane is already laid out as a pane: column `CalcColumn`, splitter `TextBoxSplitter`, `CalcResultsTextControl`, status border `CalcAggregateStatusBorder`, toggle `CalcToggleButton`, and persisted `CalcPaneWidth` / `HasCalcPaneOpen` (settings and `HistoryInfo`).

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
- Clusters that are **an editor mode** become surfaces plus a controller: Markdown, spreadsheet. They already implement `IEditorSurface` from E1; this phase moves their remaining logic out of the window and into the surface's own class and control.
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
| `calc` (first, Opus) | `CalcPane : IEditorPane` | Also introduces `IEditorPane`, `PaneDescriptor`, `IEditorPaneFactory` and registers the factory. Scroll sync with the main editor (`PassedTextControl_ScrollChanged`, `SyncCalcScrollToMain`) needs a scroll offset on the surface or session — add the minimum. Width and open state keep persisting exactly as today. |
| `transcription` | `TranscriptionController` + status-bar control | Owns `LiveAudioTranscriber`, the gate semaphore, cancellation, the status bar. Close-while-transcribing (`StopLiveTranscriptionAndCloseAsync`) becomes a session "closing" hook. |
| `localai` | `LocalAiController` | Model lifetime and "send result to new window" settings stay where they are. |
| `files` | `DocumentFileController` | File state lives on the session already; this moves open/save/prompt logic and folder OCR. |
| `markdown` | `MarkdownEditorView` + its surface | Owns `markdownOffsetMap`, theme application, task-list checkbox handling, paste handling. |
| `spreadsheet` (last, Opus) | `SpreadsheetEditorView` + its surface | Largest. Owns the `DataTable`, undo history, cell editor tracking, column width tracking, clipboard formats. Do it in several commits: model sync → selection → editing → clipboard → context menu. |

## Out of scope

- Any new pane or feature. Any visual change.
- Rendering layout from a definition (E4). Chrome changes (E6).

## Verification

- All three test executables pass after each cluster.
- `Tests/EditTextWindow*Tests.cs` keep passing. Where a test reached into window internals that moved, retarget it to the new component; do not delete coverage.
- Manual per cluster — the feature's full flow, plus: open from history with the calc pane open restores its width; closing the window mid-transcription stops cleanly; both themes.

## Done when

All six clusters are extracted, `EditTextWindow.xaml.cs` plus any remaining partials total under ~1,000 lines, and no component references `EditTextWindow` by type.

## Traps

- Moving XAML into a `UserControl` changes name scopes: `x:Name`s are no longer window fields, element-name bindings across the boundary break, and `x:Static` command references need their namespace. Build after every XAML move.
- Styles and resources defined in the window's `Resources` do not reach a control defined in another file unless they are in a merged dictionary. Check each moved fragment's `StaticResource` references.
- `Detach` must actually release things. Live transcription and the Whisper model hold native memory; a pane that leaks its subscription keeps the window alive.
- UI Automation IDs are a contract with the UI tests and with users' accessibility tools. Keep every one.
