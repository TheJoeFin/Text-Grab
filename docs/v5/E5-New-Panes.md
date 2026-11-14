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

---

## `recent` — Recent items

**Depends on:** E4. (F6 optional, for live updates.)

A searchable list of recent Edit Text Window items, grabs and files. Activating an item opens it — in this window if the document is empty or unmodified, otherwise in a new one. This is issue #592.

- Open an item in this window through `EditTextWindow.ShowHistoryItem(HistoryInfo)`, which restores its text, table, editor mode and code language together. Do not set them one by one.

- Reuse `Text-Grab/Services/TrayFlyoutItemProvider.cs` (`LoadAll`, `Merge`, `Query`) and `Text-Grab/Models/TrayFlyoutItem.cs`; the tray flyout already solved projection, search and icons. Reuse how `TrayFlyoutController` opens each item kind.
- Without F6: reload when the window is activated. With F6: subscribe to `HistoryStore.Changed`.
- Layout: **Explorer** (recent on the left; `folder-tree` joins it as a tab when that pane exists).

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
- Layout: **Mermaid**. Default layout for `.mmd` / `.mermaid` files.

## `markdown-preview` — Markdown source and preview

**Depends on:** E4.

Markdown source on the left, rendered `FlowDocument` on the right, as an alternative to the existing in-place Markdown mode.

- The source side is the editor pane in **Code mode with the Markdown language**, which gives highlighted source for free. This needs one decision in the layout, not in code: `.md` files open in Markdown mode by default (`IoUtilities.GetEditorModeForPath` checks Markdown before code), and the Markdown layout overrides that with `DefaultMode: Code`, `DefaultCodeLanguage: "MarkDown"` (`CodeLanguages.Markdown`; the capital D is AvalonEdit's spelling).
- Reuse `Text-Grab/Utilities/MarkdownFlowDocumentUtilities*.cs` for rendering and its offset map for scroll and selection sync.
- The preview is read-only. The in-place Markdown editor (`MarkdownEditorControl`) is not used by this pane; do not show both for one document.
- Debounced re-render; proportional scroll sync; clicking in the preview moves the caret in the source.
- Layout: **Markdown**. Do not change what the existing Markdown mode does.

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
| `recent` | 4.17.0-beta2 | Not started |
| `folder-tree` | 4.17.0-beta2 (stretch) or 4.18 | Not started |
| `mermaid` | 4.17.0-beta2 | Not started |
| `markdown-preview` | 4.17.0-beta2 | Not started |
| `transformer` | 5.0.0-beta1 | Not started |
| `audio` | 5.0.0-beta1 | Not started |
