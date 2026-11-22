# E5 — New panes

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | Small–medium each / Low–medium |
| Recommended model | Sonnet for `recent`, `folder-tree`, `mermaid`, `markdown-preview`; Opus for `transformer` and `audio` |
| Depends on | E4a, plus the per-pane dependencies below |
| Required decisions | D3 for `audio`; D4 for all |
| Branch | `v5/e5-pane-<name>` |
| Ships in | 4.17.0-beta2: `recent`, `mermaid`, `markdown-preview` (`folder-tree` if ready). 5.0: `transformer`, `audio`. See [Release-Plan.md](Release-Plan.md). |

Read [README.md](README.md) first.

This phase is one run per pane. Your prompt names which. The panes are independent and can be done in any order once their dependencies are met.

"E4" below means **E4a**, the pane host. No pane needs generated chrome (E4b). Until E2 has landed, a pane's actions (item 4 below) are ordinary commands on the pane's own buttons and context menu; register them in the action registry as descriptors so their IDs are pinned, and leave wiring them into menus to E2.

## What every pane delivers

1. An `IEditorPane` implementation and `IEditorPaneFactory`, registered in `AppServices`, with an ID of the form `textgrab.pane.<name>` (pinned in the ID test).
2. A view model (D4 library) holding the pane's state; the view has no logic beyond bindings.
3. A built-in layout JSON that shows the pane off, embedded alongside `classic.json`.
4. Any actions the pane contributes, registered in the action registry.
5. Tests for the view model and for any new Core / Core.Windows code.
6. `AutomationId`s on interactive elements; keyboard access to every operation; both themes.
7. `Detach` releases everything (subscriptions, timers, watchers, audio devices, models).
8. A view that survives re-parenting: AvalonDock (D1) moves it when the layout changes and when the pane is hidden or shown, so no setup or teardown in `Loaded` / `Unloaded`. A hidden pane goes idle but stays attached (see E3 *Design*).

---

## `recent` — Recent items

**Depends on:** E4. (F6 optional, for live updates.)

A searchable list of recent Edit Text Window items, grabs and files. Activating an item opens it — in this window if the document is empty or unmodified, otherwise in a new one. This is issue #592.

- Open an item in this window through `EditTextWindow.ShowHistoryItem(HistoryInfo)`, which restores its text, table, editor mode and code language together. Do not set them one by one.

- Reuse `Text-Grab/Services/TrayFlyoutItemProvider.cs` (`LoadAll`, `Merge`, `Query`) and `Text-Grab/Models/TrayFlyoutItem.cs`; the tray flyout already solved projection, search and icons. Reuse how `TrayFlyoutController` opens each item kind.
- Without F6: reload when the window is activated. With F6: subscribe to `HistoryStore.Changed`.
- Layout: **Explorer** (recent in the left pane). If `folder-tree` ships in 4.17, it replaces `recent` in that left pane: one or the other, never both at once (decided 2026-11-19). Tab headers are hidden in 4.17 (D1), so choosing between them is a Panes menu or layout choice, not a tab.

### After the `recent` run (2026-11-20, `v5/e5-pane-recent`)

- **Built:** `RecentPane` / `RecentPaneFactory` / `RecentItemsViewModel` in `Text-Grab/Editing/Panes/` (`RecentItems.cs` holds the view model, the injectable `IRecentItemsSource`, and `RecentItemOpener`, the pure rule for where an item opens). The window side is `EditTextWindow.Recent.cs`. Layout `explorer` (`LayoutIds.Explorer`): recent 260 px | editor | calc (collapsed), so Ctrl+P still works and Classic's calc pane is kept. IDs: `textgrab.pane.recent`; actions `textgrab.recent.open`, `.openInNewWindow`, `.search`, `.refresh` (descriptors only, registered by `RecentPaneActions` in the app).
- **Items** come from `TrayFlyoutItemProvider.LoadAll` over all sources (up to 100 rows; search reaches past the cap). Read on attach and on window activation (F6 is shelved). A hidden pane only marks itself stale. `IEditorPane` gained a default member `OnWindowActivated()`.
- **Deviation: "empty or unmodified".** An item opens in this window only when the window is blank: no text and no file open. `ShowHistoryItem` does not reset `OpenedFilePath` or the saved-file text, so reusing a window with a file open could later save history text over that file. Resetting file state is a follow-up if the owner wants unmodified file windows reused.
- **Opening:** double-click or Enter opens; Ctrl+Enter and the context menu's "Open in new window" always use a new window. Grabs and PDFs opened in a Grab Frame open a `GrabFrame`; a missing file offers "Remove recent item". Not copied from the tray: single click does not open.
- **Construction order:** Recent is not in Classic, so Classic windows never make it or read history (tests: `Classic_NeverMakesTheRecentPaneOrReadsHistory`, `RecentPane.CreatedCount`). `RecentPaneFactory` holds nothing, because every window resolves all factories to look up descriptors. The general "Classic renders first" order is unchanged; `.mmd` (mermaid) will hit it for the calc pane.
- **Config:** none to persist (`SaveConfig` is null; width and shown state are the host's). The search text is not kept.
- **CommunityToolkit.Mvvm 8.4.0**, not 8.4.2: 8.4.1+ require `Microsoft.Windows.SDK.NET.Ref` 10.0.22621.56 and the projects pin .48. Partial-property generation needs LangVersion preview, so the view model uses `[ObservableProperty]` fields with MVVMTK0045 (WinRT only) disabled in that file.
- **Tests** replace history for the whole assembly (`FakeRecentItemsSource`, an `AsyncLocal` list set by a module initializer), so no test reads a person's history.
- **UI tests** (`recent-pane-*` in `EditText.Tests.ps1`, helper `Set-DeterministicTextHistory`) pass: full UI command 27 pass, 0 fail, 9 skip. Two test bugs were found on the first run: the helper wrote a doubly nested JSON array (the app read it as empty history), and the tests needed the window in front. A row's `AutomationId` (`EditText.Recent.Item`, set in the item style) does not reach UI Automation; rows are list items named `Edit Text: <text>`, and `EditText.Recent.Item` only matches the list by prefix. Tests select rows by name. Unchecked by hand: both themes, keyboard-only use, touch.

## `folder-tree` — Folder tree

**Depends on:** E4, F5 (`FolderWatcherService`), F3 (`IOcrService`).

A tree of a chosen root folder. Text files open in the editor; images and PDFs OCR on demand into the editor or a new window. A pane command OCRs the whole folder.

- Config: root path, include subfolders, file filter.
- Reuse the folder OCR that already exists (`OcrAllImagesInFolder`, `OcrDirectoryOptions`, the `files` controller from E3) for "OCR this folder"; it has progress and cancellation.
- Lazy-load children on expand. Refresh from `FolderWatcherService` events, debounced.
- Use `FileUtilities` / `IoUtilities` to decide what kind of file something is rather than a new extension list. `IoUtilities.GetEditorModeForPath` already sends code files to Code mode with their language; the tree needs no logic of its own for that.
- Trap: a root such as a drive or a synced cloud folder can be huge or slow. Never enumerate recursively up front, and never OCR without an explicit user action.

## `mermaid` — Side-by-side Mermaid editor

**Depends on:** E4.

Source on the left, live diagram on the right.

- The source side is the editor pane in **Code mode**: line numbers and bracket matching help with diagram source, and a render error can point at a line. AvalonEdit has no Mermaid highlighting, so the language is Plain Text; do not write a highlighting definition in this run. `.mmd` and `.mermaid` are not in `CodeLanguages`, so those files open as Raw Text today — the Mermaid layout sets `DefaultMode: Code` rather than adding them there.
- Reuse `Text-Grab/Controls/MermaidDiagram.cs`, `Text-Grab/Utilities/MermaidImageRenderer.cs` (`RenderWithSvgAsync`), `Text-Grab/Controls/ZoomBorder.cs` for pan and zoom, and `MermaidViewerWindow` for full-screen view. All four are on `dev` (checked 2026-11-14).
- Debounce rendering (about 300 ms after the last keystroke); cancel an in-flight render when a new one starts; on a render error keep the last good diagram and show the error in a strip.
- The pane renders the **whole document** when the layout is Mermaid, or the fenced `mermaid` block under the caret when the document is Markdown.
- Actions: export SVG, export PNG, copy image, open full screen — reuse `MermaidDiagram.Save` and `CreateContextMenu`.
- Layout: **Mermaid**. Default layout for `.mmd` / `.mermaid` files: add both extensions to the built-in file-type map from [E4a](E4-Layout-Engine.md) step 11. Test that each opens in the Mermaid layout in Code mode, and that a `.md` file still opens in Markdown mode in Classic.

### After the `mermaid` run (2026-11-20, `v5/e5-pane-mermaid`)

- **Built:** `MermaidPane` / `MermaidPaneFactory` / `MermaidPaneViewModel` in `Text-Grab/Editing/Panes/`, `PaneIds.Mermaid` (`textgrab.pane.mermaid`) and the built-in layout `mermaid` (`LayoutIds.Mermaid`): the editor and the diagram pane side by side, 1\* each, `defaultMode: code`, no calc pane. Actions `textgrab.mermaid.exportSvg`, `.exportPng`, `.copyImage`, `.openFullScreen` (descriptors only, `MermaidPaneActions`; each is also a toolbar button, a context-menu item and a command palette entry). Core: `MermaidSource` (which text the pane draws), `LayoutDefaults` (the `DefaultMode` rule), and the `.mmd` / `.mermaid` entries in `LayoutFileTypeDefaults`.
- **Rendering.** The view model takes the renderer (`MermaidImageRenderer.RenderWithSvgAsync`) and the delay as delegates. A change restarts a 300 ms delay; starting a render cancels the one under way, and a result that arrives after its cancellation is dropped. A failed render keeps the last good picture and shows the message in a strip (`EditText.Mermaid.Error`); the next good render, or going back to the source already drawn, clears it. Blank text, or a Markdown caret outside every block, clears the picture and says why (not an error). The pane is idle while its view is not visible (`IsVisibleChanged`) and catches up when shown, so a layout that starts the pane collapsed still draws it once at attach. The theme is read at attach and on window activation, and a change redraws at once.
- **Reuse.** `MermaidDiagram` gained a constructor that takes a finished render (bitmap and SVG) and never draws, and `OpenViewer(Window? owner)` (the viewer's owner was `Window.GetWindow(this)`, null off the tree); `MermaidViewerWindow` takes the same owner. The pane builds one of those per call from the latest render, so Save as SVG, PNG and text, the context menu and the full-screen viewer are Markdown mode's, unchanged (`MermaidDiagramTests` pass untouched). "Copy image" is added to the pane's copy of the menu, through a clipboard seam (`MermaidPane.ImageClipboard`). `ZoomBorder` keeps one `Image` child (its `Initialize` adds handlers each time `Child` changes); wheel zoom, plus zoom in, zoom out and reset buttons for the keyboard. Panning is by mouse only.
- **Which text.** Outside Markdown mode the whole document is the diagram. In Markdown mode it is the fenced `mermaid` block at the caret (`MermaidSource.FindBlockAt`: backtick or tilde fences, any case, inside quotes and lists, longer fences closing only on as many markers). **Deviation:** the rendered Markdown editor does not report its caret as a raw offset, so `Session.Selection` there is stale, usually outside every block, and the pane then draws the *first* block. There is no selection event on `IEditorSession` either, so the pane refreshes on text change, mode change and window activation. Following the caret exactly is for the run that extracts the Markdown editor, or for `markdown-preview`, which needs the same map.
- **Deviation: no line number in the error.** Mermaider's exceptions carry no position (`MermaidParseException` has no properties), so the strip shows the message as given.
- **`DefaultMode` and `OpenPath`.** Decided and built here; the rule is in [E4's notes](E4-Layout-Engine.md#after-the-e4a-run-2026-11-20-v5e4a-pane-host). A history item's mode wins; a file's Text or Markdown mode is replaced by the layout's; a blank window takes it once; a later layout switch never changes the mode; `.mmd` is Plain Text, never detected. `markdown-preview` needs no more than `defaultMode: code` and `defaultCodeLanguage: "MarkDown"` in its JSON, and `.md` keeps opening in Classic because `.md` is not in the map.
- **Construction order.** Fixed for built-in layouts, as [E4 notes](E4-Layout-Engine.md#after-the-e4a-run-2026-11-20-v5e4a-pane-host): the window is told its layout before `InitializeComponent`, so a window for an `.mmd` file renders the Mermaid layout first and never makes the calc pane. Anyone adding another mapped layout gets this for free as long as the layout is built in and the entry point passes the file path to `new EditTextWindow(new LayoutRequest(...))`; a new entry point that opens a file in a new window must do so. Classic, `.txt`, `.md` and the hand-written window never make a Mermaid pane or its view model (`MermaidPane.CreatedCount`).
- **Config:** none to persist (`SaveConfig` is null). **No new packages.**
- **Tests:** `MermaidPaneViewModelTests` (debounce, cancellation, last good diagram, theme, idle, release, and one run against the real renderer; the manual delay and renderer are `ManualMermaid`), `MermaidPaneTests` (pane, factory, palette, automation ids, zoom, the layout), `EditTextWindowMermaidLayoutTests` (every row of the rule, the construction order, history, the pane layout off), and in `Tests.Core` `MermaidSourceTests`, `LayoutDefaultsTests` and the map and resolver tests. UI tests `mermaid-*` in `EditText.Tests.ps1` (fixtures in `UiTests/Fixtures/Mermaid`). Editing in the UI tests uses `Set-UiTestValue` on `EditText.CodeEditor` (AvalonEdit answers ValuePattern); `Focus-UiTestElement` on it fails with an internal error. The diagram's `HelpText` is the start of the source it was drawn from, which is what the tests wait on instead of pixels. Unchecked by hand: both themes, a live theme switch, touch, keyboard-only use, the viewer from the pane on a second monitor, and a very large diagram.

## `markdown-preview` — Markdown source and preview

**Depends on:** E4.

Markdown source on the left, rendered `FlowDocument` on the right, as an alternative to the existing in-place Markdown mode.

- The source side is the editor pane in **Code mode with the Markdown language**, which gives highlighted source for free. This needs one decision in the layout, not in code: `.md` files open in Markdown mode by default (`IoUtilities.GetEditorModeForPath` checks Markdown before code), and the Markdown layout overrides that with `DefaultMode: Code`, `DefaultCodeLanguage: "MarkDown"` (`CodeLanguages.Markdown`; the capital D is AvalonEdit's spelling).
- Reuse `Text-Grab/Utilities/MarkdownFlowDocumentUtilities*.cs` for rendering and its offset map for scroll and selection sync.
- The preview is read-only. The in-place Markdown editor (`MarkdownEditorControl`) is not used by this pane; do not show both for one document.
- Debounced re-render; proportional scroll sync; clicking in the preview moves the caret in the source.
- Layout: **Markdown**. Do not change what the existing Markdown mode does.

### After the `markdown-preview` run (2026-11-20, `v5/e5-pane-markdown-preview`)

- **Built:** `MarkdownPreviewPane` / `MarkdownPreviewPaneFactory` / `MarkdownPreviewViewModel` / `MarkdownPreviewSync` in `Text-Grab/Editing/Panes/`, `PaneIds.MarkdownPreview` (`textgrab.pane.markdownPreview`, camelCase like `workflowGraph`) and the built-in layout `markdown-preview` (`LayoutIds.MarkdownPreview`, title "Markdown preview"): editor and preview side by side, 1\* each, `defaultMode: code`, `defaultCodeLanguage: "MarkDown"`, no calc pane. The layout ID is `markdown-preview`, not `markdown`, so `--layout markdown-preview` matches the pane's name. One action descriptor, `textgrab.markdownPreview.goToSource` (Enter in the preview; also a command palette entry). **The file-type map is untouched**: `.md` still opens in Classic, in Markdown mode (pinned by `BuiltInMap_HasExactlyTheMermaidEntries` and by tests and UI tests that open an `.md` without a layout). No change was needed to `DefaultMode` handling: `LayoutDefaults` already turns an `.md` file opened in this layout into Code mode, and the file's own extension gives it the `MarkDown` language (a `.txt` gets it from the layout's `DefaultCodeLanguage`).
- **Rendering.** `MarkdownFlowDocumentUtilities.CreateFlowDocument` + `ApplyTheme`, used read-only, into a read-only `RichTextBox` (not `MarkdownEditorControl`). A `FlowDocument` belongs to the UI thread, so the renderer is a synchronous delegate the view model calls after a 300 ms cancellable delay; there is no render on a worker thread, so a very large document blocks the UI for as long as Markdown mode does. A build that throws keeps the last good document and shows the message in a strip (`EditText.MarkdownPreview.Error`). The pane is idle while hidden (`IsVisibleChanged`), re-reads the theme on window activation, and lets go of the document on `Detach`. Table columns are fitted after layout with `FitTableColumns`, as Markdown mode does. Task-list boxes are made non-interactive (they would toggle in the preview and never reach the source). Links go through `ExternalLinkLauncher`. The open file's path comes from the window (`FilePathProvider`, set in `AttachLayoutPane`), so relative images resolve; `IEditorSession` was not changed.
- **In Markdown mode the pane shows a notice** ("The editor is showing the rendered Markdown itself...") and renders nothing, so one document is never shown twice. It renders again when the mode goes back to anything else.
- **Deviation: scroll sync is proportional only.** `FirstVisibleLine / (lines - 1)` of the source maps to the same fraction of the preview's scroll range, re-applied after each render (a new document starts at the top). It is one way, source to preview. Anchoring by the offset map would be more exact, but `BuildOffsetMap` measures the text of every run and the sync runs on every scroll event. Known limit: the editor's last *visible-at-top* line is less than the last line, so the preview does not reach its bottom until the source is scrolled past it; and headings, images and diagrams are not the same height as their source lines, so the two drift between the ends.
- **Click to caret.** A mouse-up in the preview with no selection (a drag that selects text is for copying), or Enter, moves the caret in the source: `MarkdownPreviewSync.RawOffsetAt` is the inverse of the utilities' map, built on their public records and made the first time a click needs it, not per render. It matches the clicked paragraph to its `AnchorParagraph`, then interpolates inside the matching run, so with stripped syntax such as `**` it lands in the right word, not always on the right letter. Positions in something the map does not cover (a footnote list) do nothing. Links are skipped. The move uses `IEditorSession.SelectInEditor`, which focuses the editor (it is mouse-up and Enter only, so arrow keys in the preview never steal focus) and suppresses scroll sync until idle so the preview does not scroll back.
- **Config:** none to persist (`SaveConfig` is null). **No new packages.**
- **Tests:** `MarkdownPreviewViewModelTests` (debounce, last good document, notice, theme, idle, release; `ManualPreview`), `MarkdownPreviewPaneTests` (pane, factory, palette, automation ids, the real rendering, task boxes, the offset inverse, click to caret, which windows make it, the layout), `EditTextWindowMarkdownPreviewLayoutTests` (Code mode and `MarkDown` for `.md`, `.txt`, a blank window and a text window; the language surviving `Window_Loaded`; `.md` with no layout staying in Classic and Markdown mode; history; the pane layout off), plus the layout twins in `Tests.Core` and the built-in list in `Tests.Core.Windows`. The exact lists of pane IDs and action registries in older tests gained the new entries. UI tests `markdown-preview-*` in `EditText.Tests.ps1` (fixture `UiTests/Fixtures/MarkdownPreview/notes.md`; `Copy-DeterministicMermaidFixture` gained a `-Folder` parameter, default `Mermaid`). The preview's `HelpText` is the start of its rendered text, which is what the tests wait on. Unchecked by hand: both themes, a live theme switch, touch, keyboard-only use, a very large document, images and diagrams in the preview, scroll feel.

## `transformer` — Side-by-side transformer (Opus)

**Depends on:** E4, A1, W1.

Input on the left (the document or its selection), an editable ordered list of transform stages in the middle, live output on the right.

- The stage list **is a linear `WorkflowDefinition`**: manual trigger → stage 1 → … → stage N. Run it with W1's `WorkflowRunner`; do not write a second execution path.
- Add stage: pick from the action registry's `ITextTransform`s, grouped by category, with search. A stage with a `ConfigType` shows its parameters inline.
- Reorder, disable, remove stages. Re-run debounced on any change to input or stages; show per-stage errors from `WorkflowRunResult` against the stage.
- Actions: **Apply** (replace the document or selection with the output, as one undo step); **Save as workflow** (to the workflow store); **Save as post-grab action**; **Open in graph editor** (when W3 exists).
- Guard: cap input size for live preview and fall back to a manual Run button above it; never run a stage flagged `RequiresCopilotPlus` on every keystroke.
- Layout: **Transformer**.

## `audio` — Audio transcript (Opus)

**Depends on:** E4, D3.

A waveform with transport controls. During playback the matching text is highlighted in the editor; clicking text seeks the audio.

The data this needs does not exist yet. `AudioTranscriptionUtilities.TranscribeAudioFileAsync` returns one flat string; Whisper's per-segment timing (`SegmentData.Start` / `.End`) is discarded except for an optional `[hh:mm:ss]` text prefix. Build in this order:

1. **`TranscriptDocument`** in `Text-Grab.Core` (`Text_Grab.Core.Editing`): an ordered list of `TranscriptSegment(TimeSpan Start, TimeSpan End, TextSpan Range)` plus the audio source path. Pure, with tests.
2. **Anchors that survive edits:** a method that takes a `TextEdit` (`Text_Grab.Core.Editing`, added with Code mode — the session already produces one per edit in `ApplyEditFromCodeEditor`, and `TextEdit.Between` derives one from any two texts) and shifts or resizes segment ranges. Edits inside a segment resize it; edits spanning segments merge the affected ranges conservatively. Property-style tests: after any sequence of edits, ranges stay ordered, non-overlapping and within the text.
3. **A transcription API that returns it:** a new overload in `Text-Grab.Core.Windows/Utilities/AudioTranscriptionUtilities.cs` returning a `TranscriptDocument` alongside the text. The existing string-returning method stays and becomes a projection of the new one. Do the same for `LiveAudioTranscriber` (it already tracks `SpeechSegment(Start, End)` internally) only if it falls out naturally; otherwise file-based transcription first.
4. **Waveform peaks:** a headless function in Core.Windows that reduces an audio file to min/max peak pairs at a requested resolution, using NAudio (already referenced). Streamed, cancellable, tested against a short fixture.
5. **Playback service** in Core.Windows over NAudio: play, pause, seek, rate, and a position event (~30 Hz).
6. **Range highlighting (D3):** add `Highlight(IReadOnlyList<TextSpan>)` / `ClearHighlight()` to `IEditorSurface`. Per D3 as revised on 2026-11-14, implement it on `CodeSurface` with an AvalonEdit `IBackgroundRenderer` — `SelectionOccurrencesRenderer` in `Text-Grab/Controls/CodeEditorRenderers.cs` already draws backgrounds behind ranges and is the pattern to copy, including how it handles wrapped lines and collapsed regions. The other three surfaces implement the two methods as no-ops. The Transcription layout sets `DefaultMode: Code` with Plain Text so the highlight is available. Check two things early and report them: playback-rate highlight updates (~30 Hz) must not re-run folding or re-highlight the document, and the highlight must stay readable over the selection color in both themes. If D3 is set back to the adorner approach, spike it on the `TextBox` surface first and report its limits (wrapping, scrolling, selection overlap) before building on it.
7. **The pane:** waveform drawing, playhead, click and drag to seek, keyboard transport, current-segment highlight, click-in-text seeks.
8. **Persistence:** save the `TranscriptDocument` as a sidecar next to the saved text file and with the history item, following how `HistoryFileUtilities` stores word-border sidecars. A document with no sidecar opens without the pane's sync features rather than failing.

Layout: **Transcription** (audio pane on top, text below, live-transcription controls in the bottom bar). Default layout when an audio or video file is opened.

Traps: audio decode of long files is slow and memory-hungry — peaks must stream. Whisper segment times are relative to the processed audio, which the existing code resamples and may chunk; confirm times map to the original file. Holding an audio output device keeps other apps from exclusive mode; release it on pause-idle and in `Detach`.

---

## Verification (every pane)

- All three test executables pass.
- Manual: open the pane's built-in layout with `--layout <id>`; exercise every action; close and reopen and confirm the pane's config persisted; both themes; keyboard only.
- Manual: the Classic layout is unaffected (the pane does not load, its services are not constructed).

## Done when (per pane)

The pane is registered, ships with a layout, has tests, and appears in the phase report with its manual checks. Update the pane's row in this file with `Done (<commit>)`.

| Pane | Ships in | Status |
|---|---|---|
| `recent` | 4.17.0-beta2 | Done (191b7f4) |
| `folder-tree` | 4.17.0-beta2 if the rest lands smoothly, otherwise 4.18 | Not started |
| `mermaid` | 4.17.0-beta2 | Done (b6cf46e) |
| `markdown-preview` | 4.17.0-beta2 | Done (see the commit that adds this line) |
| `transformer` | 5.0.0-beta1 | Not started |
| `audio` | 5.0.0-beta1 | Not started |
