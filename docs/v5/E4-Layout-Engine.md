# E4 — Layout engine and the Classic layout

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | E4a: Medium–large / Medium. E4b: Large / **Highest in the track** (parity with the hand-written window) |
| Recommended model | Opus for both |
| Depends on | **E4a:** E3 `calc`. **E4b:** E2 (`final` run), all of E3, E4a |
| Required decisions | D1 (docking: AvalonDock v5, confirmed), D5 (layout scope, confirmed) |
| Unblocks | E4a: E5, E6 `picker` and `defaults`, W3. E4b: E6 `ribbon` and `editor` |
| Branches | `v5/e4a-pane-host`, then `v5/e4b-chrome` |
| Ships in | E4a: 4.17.0-beta2. E4b: 4.18, behind a setting. See [Release-Plan.md](Release-Plan.md). |

Read [README.md](README.md) first.

This phase has two parts, each its own branch and pull request. **Do only the part named in your prompt.**

It was one phase until 2026-11-14. It was split so that panes can ship in 4.17 without first converting every command (E2) and generating every menu: E4a renders only the **dock tree** — the editor area and the panes around it — and leaves the menu bar, bottom bar and status bar as hand-written XAML. E4b generates those too.

E4a was revised on 2026-11-19, after D1 was confirmed as AvalonDock v5. In 4.17, AvalonDock only replaces what the window already does.

## Goal

The Edit Text Window builds itself from a layout definition. After E4a, a layout decides which panes surround the editor. After E4b, it also decides the chrome, and a "window type" is entirely a JSON file. **Classic** reproduces today's window at both stages.

## Current state (verify)

- Four editors sit in the editor area, switched by `EtwEditorMode`: `PassedTextControl` (`TextBox`), `MarkdownEditorControl` (`RichTextBox`), `SpreadsheetDataGrid`, and since PR #710 `CodeEditorControl` (AvalonEdit, mode `Code`). The four stay together as one unit, the editor pane (`textgrab.pane.editor`); a layout does not arrange them individually.
- `EditTextWindow.xaml` is 1,901 lines with 225 `MenuItem`s (2026-11-19, `dev` @ `5e3b889`). Row by row it places: the title bar (`UiTitleBar`); the menu bar (`EditorMenu`) with the spreadsheet and calc toggle buttons beside it; the Markdown formatting bar (`MarkdownToolbar`, shown in Markdown mode); the editor area; the bottom bar (`BottomBarScrollViewer` / `BottomBar`) with the status strips (including `CodeLanguageButton`, shown in Code mode). Overlays span the rows: `CommandPaletteOverlay`, `LoadingStack`, `PendingFileCloseDialogHost`.
- Before E3 `calc`, the editor area is a two-column `Grid`. Column 0 is `ReadingEditorHost`: the four editors plus the "Pick up where you left off" bookmark (`ResumeReadingButton`) over them. That whole grid is the editor pane. Column 1 is `CalcColumn` (200 px in XAML), holding `CalcResultsTextControl` (`MinWidth` 150), `CalcAggregateStatusBorder`, the copy and info buttons and `CalcInfoPopup`. The splitter (`TextBoxSplitter`, 5 px) and a shadow (`CalcPaneShadow`) sit at the left edge **inside** column 1, not in a column of their own. Double-clicking the splitter toggles the pane between a 50/50 star split and closed.
- After E3 `calc` (done, `0fb4420`): `IEditorPane`, `PaneDescriptor`, `IEditorPaneFactory` exist and `CalcPaneFactory` is registered. Column 1 now holds `CalcPaneControl` (the `CalcPane` user control, which holds the results box, aggregate status, buttons and popup), with `TextBoxSplitter` and `CalcPaneShadow` still the window's, drawn over its left edge. The window sets `CalcPane.IsOpen` and binds its `ResultsFontSize` and `ShowErrors`; see E3's *After the `calc` run* for the full contract. After all of E3 and E2 (needed for E4b only): every cluster is a component, and actions are invoked by ID through `EditorActionInvoker` and bind through `ActionCommand`.
- Persisted window state today: position and size ("restore position"), `CalcPaneWidth`, calc pane open, wrap, font, margins, always on top, bottom bar hidden, bottom-bar button list, and per history item: editor mode, code language (`HistoryInfo.CodeLanguage`), calc pane state.
- UI test infrastructure: `UiTests/TextGrab.AutomationHost`, `.github/workflows/interactive-ui.yml`, `Tests/UiAutomationContractTests.cs`, and `AutomationId`s such as `EditText.Menu.Selection`, `EditText.MarkdownEditor`, `EditText.CodeEditor`.

## Design

**Model** in `Text-Grab.Core`, namespace `Text_Grab.Core.Layouts` — records, `System.Text.Json`, versioned, no WPF:

```csharp
public sealed record EtwLayoutDefinition(string SchemaVersion, string Id, string Name,
    DockNode Root, EtwEditorMode? DefaultMode, string? DefaultCodeLanguage, WindowOptions? Window,
    // E4b. Null means "the window's built-in chrome", which is all E4a can render.
    ChromeDefinition? Chrome = null, IReadOnlyList<string>? BottomBarActionIds = null,
    IReadOnlyList<string>? StatusItemIds = null);

[JsonPolymorphic] public abstract record DockNode;
public sealed record SplitNode(DockOrientation Orientation, IReadOnlyList<DockChild> Children) : DockNode;
public sealed record DockChild(DockNode Node, double Size, DockSizeUnit Unit /*Star|Pixel*/, bool Collapsed);
public sealed record PaneNode(string PaneId, JsonElement? Config) : DockNode;   // "textgrab.pane.editor" is the editor area
public sealed record TabGroupNode(IReadOnlyList<PaneNode> Tabs, int SelectedIndex) : DockNode;

public sealed record WindowOptions(bool AlwaysOnTop, bool RestorePosition, bool ShowBottomBar);

// E4b
public enum ChromeKind { MenuBar, Ribbon, None }
public sealed record ChromeDefinition(ChromeKind Kind, IReadOnlyList<ChromeGroup> Groups);
public sealed record ChromeGroup(string Title, string? AccessKey, IReadOnlyList<ChromeItem> Items);
public sealed record ChromeItem(ChromeItemKind Kind /*Action|Separator|Submenu|DynamicList*/,
    string? ActionId, string? Title, string? DynamicSourceId, IReadOnlyList<ChromeItem>? Children);
```

- The chrome members are **optional from the first schema version**, so a layout written by 4.17 (E4a) loads unchanged once E4b exists, and no migration is needed between them.
- **The model stays library-agnostic (D1).** No AvalonDock type appears in Core or in a layout file, and AvalonDock's own XML layout serializer is not used: layouts are Text Grab's JSON in the store (a fixed decision). The renderer converts between the two.
- `DefaultMode` null means "whatever the file or history item says". `DefaultCodeLanguage` applies only when the mode is `Code`; it is an AvalonEdit definition name as listed in `CodeLanguages`, and an unknown name falls back to detection.
- `DynamicList` chrome items cover menus filled at open time (recent items, languages, patterns, templates, web searchers, **code languages**). Each has a `DynamicSourceId` resolved app-side to the existing population code.
- **Store:** `ILayoutStore` in Core; `FileLayoutStore` in Core.Windows writing `layouts/<id>.json` through `FileUtilities`' packaged/unpackaged storage (W1's workflow store will follow the same approach). Built-in layouts are embedded resources and are read-only; "customize" copies one into the store.
- **Renderer** (app, `Text_Grab.Editing.Layout`): `LayoutRenderer` turns a `DockNode` tree into an AvalonDock layout in one `DockingManager` (D1), through the pane factories, and writes pane sizes and collapsed state back to the live layout instance. The mapping is in E4a below. In E4b it also turns a `ChromeDefinition` into a menu bar and the bottom-bar list into buttons, through `ActionCommand`; the chrome sits outside the `DockingManager` and does not involve AvalonDock.
- **Scope (D5):** each window holds its own live copy of a layout. Resolution order when a window opens: an explicit layout requested by the caller → the layout stored with the history item → the default for the file type → the global default (Classic).
- **History:** the layout ID is stored on `HistoryInfo` as an optional string, omitted when null. Not an enum: `EditorMode` is written by name, so 4.16 cannot read an entry saved as `Code`, and a layout ID must not repeat that.
- **Entry points:** `WindowUtilities` gains an optional layout ID when opening an Edit Text Window; `--layout <id>` on the command line; `text-grab://edit-text?layout=<id>` (parsed in `ProtocolUtilities`).

---

## E4a — Pane host and pane layouts (Opus)

Requires E3 `calc` merged. Does **not** require E2 or the other E3 clusters.

**D1 in this release (confirmed 2026-11-19):** AvalonDock v5, used only to do what the window does today. Panes don't float, auto-hide or drag, and they have no chrome: no title bars, tabs, or pin and close buttons. The calc pane opens, closes and resizes exactly as it does now, apart from the splitter double-click (see *Intentional differences*). Turning on any AvalonDock feature beyond that is a later decision, not part of this run.

### AvalonDock setup

- **Package:** `Dirkster.AvalonDock` 5.0.x (Ms-PL; targets `net10.0-windows`). The 5.x line dates from 2026-08-12 and 5.0.2 from 2026-11-12, while most documentation and samples online are for 4.x, so check API names against the 5.x source. No theme package: start from the generic theme in the main assembly. Add the package to `BUILT-WITH.md` and to the in-app list (`ThirdPartyNoticeUtilities` and its test in `Tests.Core`), and report it for the MSIX package build.
- **One `DockingManager`** fills the editor-area row. The title bar, menu bar, Markdown formatting bar, bottom bar, status strips and overlays stay hand-written XAML outside it.
- **Mapping:**

  | Model | AvalonDock |
  | --- | --- |
  | `SplitNode` | `LayoutPanel` with the same `Orientation` |
  | `PaneNode("textgrab.pane.editor")` | A `LayoutDocumentPane` holding one `LayoutDocument`. Its content is the existing `ReadingEditorHost` grid: the four editors and the reading bookmark, moved as one unit. |
  | Any other `PaneNode` | A `LayoutAnchorablePane` holding one `LayoutAnchorable`: `ContentId` is the pane ID, `Title` comes from the `PaneDescriptor`, content is `IEditorPane.View`. |
  | `TabGroupNode` | A `LayoutAnchorablePane` holding only the selected tab. Tab headers are hidden in 4.17, so the other tabs stay in the definition, are not constructed, and survive a save. No built-in 4.17 layout uses a tab group. |
  | `DockChild.Size` and `Unit` | The pane's `DockWidth` or `DockHeight` (along the parent's orientation) as a pixel or star `GridLength`, with a minimum from the pane (`DockMinWidth` / `DockMinHeight`; the calc pane's is 150 today). |
  | `DockChild.Collapsed` | The anchorable is hidden with `Hide()`, which keeps its place and size for `Show()`. |

- **Lock-down.** On the `DockingManager`: `ShowNavigator = false` (otherwise Ctrl+Tab opens AvalonDock's pane switcher, even with focus in an editor) and `ShowSystemMenu = false`. On every anchorable: `CanFloat`, `CanAutoHide`, `CanMove`, `CanClose`, `CanHide`, `CanDockAsTabbedDocument` and `CanShowOnHover` all false. On the editor's document: `CanFloat`, `CanMove` and `CanClose` false. The hide path that code calls (`Hide` → `ExecuteHideCommand` → `HideAnchorable`) does not check `CanHide` (checked in the 5.x source), so the window can still collapse a pane that the user cannot.
- **Hidden chrome.** Custom templates, scoped to the Edit Text Window's `DockingManager`, for `LayoutAnchorablePaneControl`, `LayoutAnchorableControl` and `AnchorablePaneTitle` (no title bar or buttons) and `LayoutDocumentPaneControl` (no tab strip). Nothing hidden may stay focusable, in the Tab order, or invokable through UI Automation.
- **Theme.** The resize bar (`LayoutGridResizerControl`) and pane backgrounds use WPF-UI brushes through `DynamicResource`, so light, dark and a live theme switch follow the app. Match today's look: a 5 px splitter in the base background color.
- **Write-back.** When a resize ends and when a pane is shown or hidden, read `DockWidth` / `DockHeight` and the hidden state back into the window's live layout. Keep writing `CalcPaneWidth`, `CalcShowPane` and the history item's calc fields as today, from the pane's actual width.

### Steps

1. **Capture a baseline before changing anything.** Dump the UI Automation subtree of the editor area and the calc pane (control type, name, `AutomationId`, enabled state, order) in Text, Markdown, Spreadsheet and Code modes, calc pane open and closed, plus the Tab order through them. The harness has no tree dump yet. Add a helper to `UiTests/TextGrab.UiTestHarness.psm1` that wraps `winapp ui inspect <selector> --depth N` and drops bounds and slugs, which change between runs, so the fixtures compare. Commit the dumps as test fixtures.
2. Model records + JSON round-trip tests in `Tests.Core`, including: unknown pane ID, unknown schema version, a file with no chrome members, a file *with* chrome members (they must survive a load/save cycle untouched even though E4a cannot render them), and a tab group whose unselected tabs survive.
3. `ILayoutStore` + `FileLayoutStore` + tests.
4. **Author `classic.json`:** the editor pane, and the calc pane to its right, pixel-sized and collapsed, matching today.
5. **Add AvalonDock and write `LayoutRenderer`** for the dock tree, as in *AvalonDock setup*: the package and its notices, the mapping, lock-down, hidden chrome, theme and write-back. The window's XAML keeps its menu bar, Markdown formatting bar, bottom bar, status strips and overlays; the `DockingManager` fills the region between them.
6. Put the rendered path behind a setting that defaults **off**. With it on, the window uses the renderer; off, the hand-written XAML. With it on, every calc toggle (`ShowCalcPaneMenuItem`, `CalcToggleButton`, Ctrl+P, the pane's Hide Pane item) collapses and shows the calc pane through the live layout.
7. **Parity test:** with the setting on, the content of the editor pane and the calc pane, and the Tab order through them, match the step-1 fixtures. AvalonDock's own container elements (the `DockingManager` and its panel, pane and document controls) are expected additions: list them in the test as accepted, and fail on anything else. Record every other difference under *Intentional differences* below, with a reason.
8. Migrate persisted state: on first use, the existing calc width and open state become the user's live Classic layout. The old settings keep being written so a downgrade still works.
9. **Measure, then flip the default.** With the setting off and on, measure the Edit Text Window's open time (until the editor accepts input) and its working set, on x64 and ARM64, with a short and a large document, and put the numbers in the phase report. Then flip the setting's default to **on**, unless the owner decides the cost is too high. Leave the old XAML path in place for one release.
10. Entry points: `WindowUtilities` parameter, `--layout`, protocol parameter, the history field, with tests for the parsing and for an older-shaped history file.
11. **Built-in file-type defaults.** Implement the "default for the file type" step of the resolution order (Design, **Scope**) with a fixed, read-only map from file extension to layout ID, in `Text_Grab.Core.Layouts` beside the model. It ships **empty** in E4a, so every file still resolves to Classic and opens exactly as before. E5 runs add the entries for their own layouts; in 4.17 the only entries are `.mmd` and `.mermaid` (E5 `mermaid`). Do not map `.md`: Markdown files keep opening in Markdown mode in Classic, and the Markdown layout stays a choice in the Layouts menu. There is no settings UI; E6 `defaults` (5.0) layers user choices on top of this map. Tests: a history item's layout beats the file type; an extension that is not in the map resolves to Classic; a mapped layout ID that is missing from the store falls back to Classic.

### Intentional differences (setting on)

- **The splitter double-click is gone.** Today it toggles the calc pane between a 50/50 split and closed. D1 allows dropping it; say so in the release notes.
- **The calc pane's shadow** (`CalcPaneShadow`): rebuild it inside the calc pane's view or drop it, and record which.
- **AvalonDock's container elements** appear in the UI Automation tree; the parity test lists them.
- Add any others found during the run here, each with its reason.

### Out of scope

- Generating menus, the bottom bar or status items (E4b). A layout that carries chrome members is accepted and ignored.
- Any layout other than Classic (E5 adds them with their panes). A layout picker (E6).
- Any AvalonDock feature beyond today's behavior: dragging panes to dock them, floating panes, auto-hide, pane title bars and tabs, tab tear-off. Each needs its own decision before it is turned on (D1).
- AvalonDock's XML layout serializer.

### Done when

The default window's dock area is rendered from `classic.json` through AvalonDock, the parity test passes, existing users' calc pane state carries over, the open-time and memory numbers are in the phase report, and a second layout file dropped in the store opens with `--layout`.

---

## E4b — Chrome from the definition (Opus)

Requires E2's `final` run, every E3 cluster, and E4a.

### Steps

1. **Capture the chrome baseline.** Dump the whole window's UI Automation tree (adding access key to the E4a fields) in all four modes, and the full menu tree with shortcut text. In Code mode, capture it with the code editor's find bar open and closed: Toggle Case shows `Shift + F3` only while it is closed.
2. Chrome records' round-trip tests, including an unknown action ID (load succeeds; rendering shows a placeholder and logs; nothing is deleted).
3. **Extend `classic.json`** from the current XAML: every menu, submenu, separator and dynamic list, in order, with access keys; the bottom bar; the status items.
4. `LayoutRenderer`: chrome, then bottom bar and status items.
5. Behind its own setting, default **off**. A layout with no chrome members keeps using the hand-written XAML regardless.
6. **Parity test** against the step-1 fixtures, as in E4a.
7. Migrate persisted state: bottom-bar buttons and window options become part of the user's live Classic layout. The old settings keep being written.
8. Flip the default to **on** when the release plan says so. Leave the old XAML path in place for one release after that; add a note here saying when it can be deleted.

### Out of scope

- Ribbon chrome, a layout editor (E6). `ChromeKind.Ribbon` exists in the model but renders as a menu bar for now.

### Done when

The default Edit Text Window, chrome included, is rendered from `classic.json`, the parity test passes, and existing users' bottom bar and window options carry over.

---

## Verification

- All three test executables pass, including the parity test for the part you did.
- Manual, setting on (E4a):
  - The calc pane opens, resizes, and restores its width and open state from settings and from history.
  - Calc scrolling follows the editor in Text and Code modes.
  - Switching between all four modes leaves the panes alone.
  - Ctrl+Tab in each editor does what it did before; AvalonDock's pane switcher never opens.
  - No pane can be dragged, floated, auto-hidden or closed, by mouse, touch or keyboard.
  - Showing or hiding the calc pane leaves keyboard focus in the editor.
  - The command palette, loading overlay and pending-close dialog still draw over the panes.
  - The calc pane resizes by touch (Surface).
  - Both themes, including a live switch; 100% and 200% DPI.
- Manual, setting on (E4b): every menu and submenu opens and is in the same order; every access key (`Alt` + letter) and shortcut works, **including from inside the code editor**, which has its own key bindings; tab order through the window is unchanged; bottom-bar customization from the settings page still applies; a screen reader announces the same names (Narrator spot check).
- Manual, setting off: the old path still works.

## Traps

- **Dynamic menus** are populated on `SubmenuOpened` and cleared on close to keep memory down. The renderer must keep that laziness; do not build every submenu up front.
- Access keys and `InputGestureText` are part of parity. So is the difference between a menu item that is disabled and one that is hidden. Some are **state-dependent**: the Code Language and Show Whitespace items apply only in Code mode, and Toggle Case's gesture text changes with the find bar.
- AvalonEdit handles keys before the window does. `CodeEditor` removes its Ctrl+D and Ctrl+I bindings so the window's Duplicate Line and Isolate Selection work; a generated shortcut that collides with another AvalonEdit binding will silently do the editor's thing instead. Test every generated gesture with focus in the code editor.
- **Pane sizes.** AvalonDock sizes a pane by `DockWidth` / `DockHeight` (pixel or star) with `DockMinWidth` / `DockMinHeight`; the calc pane today uses a pixel width with a remembered last width. Model exactly that (`DockSizeUnit`), and check that hiding and showing a pane brings back its width, or resizing and toggling will feel different.
- A layout file is user-editable input. Unknown IDs, a missing editor pane (render one anyway), sizes of zero or negative, and absurd nesting depth must all render something usable rather than throw.
- **Re-parenting.** AvalonDock moves content between containers when the layout changes and when a pane is hidden or shown. That raises `Unloaded` and `Loaded` again on everything inside, the editors included. Nothing may be set up or torn down for good in those events: the host calls `IEditorPane.Attach` / `Detach`, never `Loaded` / `Unloaded`. `CodeEditor` re-applies its theme on `Loaded`, `MermaidDiagram` re-renders, and `RichTextBoxTouchBehavior` only resets, so those three already cope (checked 2026-11-19).
- **Hidden panes stay alive.** A hidden anchorable keeps its content in `LayoutRoot.Hidden`. Hiding a pane is not detaching it: a hidden pane must go idle (calc already skips evaluation when it is not visible).
- **Focus.** AvalonDock tracks the active content and moves keyboard focus when content activates. Check that showing or hiding a pane, the command palette's focus restore, and `IEditorSession.SelectAndShow` all leave focus where they do today.
- **Native windows over overlays.** Auto-hide's flyout is a separate native window (`LayoutAutoHideWindowControl : HwndHost`, checked in the 5.x source), so it would draw over the command palette, the loading overlay and the pending-close dialog. It is off in 4.17; whoever turns it on later must solve that first. Floating windows bring the same problem, plus their own theming.
- **Keys.** The only key handling in `DockingManager` itself is Ctrl+Tab for the pane switcher, which `ShowNavigator = false` turns off. Still test every window shortcut with focus in each editor.
