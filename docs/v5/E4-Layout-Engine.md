# E4 — Layout engine and the Classic layout

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | E4a: Medium–large / Medium. E4b: Large / **Highest in the track** (parity with the hand-written window) |
| Recommended model | Opus for both |
| Depends on | **E4a:** E3 `calc`. **E4b:** E2 (`final` run), all of E3, E4a |
| Required decisions | D1 (docking), D5 (layout scope) |
| Unblocks | E4a: E5, E6 `picker` and `defaults`, W3. E4b: E6 `ribbon` and `editor` |
| Branches | `v5/e4a-pane-host`, then `v5/e4b-chrome` |
| Ships in | E4a: 4.17.0-beta2. E4b: 4.18, behind a setting. See [Release-Plan.md](Release-Plan.md). |

Read [README.md](README.md) first.

This phase has two parts, each its own branch and pull request. **Do only the part named in your prompt.**

It was one phase until 2026-11-14. It was split so that panes can ship in 4.17 without first converting every command (E2) and generating every menu: E4a renders only the **dock tree** — the editor area and the panes around it — and leaves the menu bar, bottom bar and status bar as hand-written XAML. E4b generates those too.

## Goal

The Edit Text Window builds itself from a layout definition. After E4a, a layout decides which panes surround the editor. After E4b, it also decides the chrome, and a "window type" is entirely a JSON file. **Classic** reproduces today's window at both stages.

## Current state (verify)

- Four editors sit in the editor area, switched by `EtwEditorMode`: `PassedTextControl` (`TextBox`), `MarkdownEditorControl` (`RichTextBox`), `SpreadsheetDataGrid`, and since PR #710 `CodeEditorControl` (AvalonEdit, mode `Code`). The four stay together as one unit, the editor pane (`textgrab.pane.editor`); a layout does not arrange them individually.
- `EditTextWindow.xaml` is 1,636 lines with 220 `MenuItem`s. It places: title bar (`UiTitleBar`), menu bar, the editor area, the calc pane with its splitter, the bottom bar (`BottomBarScrollViewer` / `BottomBar`), status strips (including `CodeLanguageButton`, shown in Code mode), overlays (`LoadingStack`, `PendingFileCloseDialogHost`).
- After E3 `calc`: `IEditorPane`, `PaneDescriptor`, `IEditorPaneFactory` exist and `CalcPane` is registered. After all of E3 and E2 (needed for E4b only): every cluster is a component, and actions are invoked by ID through `EditorActionInvoker` and bind through `ActionCommand`.
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
- `DefaultMode` null means "whatever the file or history item says". `DefaultCodeLanguage` applies only when the mode is `Code`; it is an AvalonEdit definition name as listed in `CodeLanguages`, and an unknown name falls back to detection.
- `DynamicList` chrome items cover menus filled at open time (recent items, languages, patterns, templates, web searchers, **code languages**). Each has a `DynamicSourceId` resolved app-side to the existing population code.
- **Store:** `ILayoutStore` in Core; `FileLayoutStore` in Core.Windows writing `layouts/<id>.json` through `FileUtilities`' packaged/unpackaged storage (W1's workflow store will follow the same approach). Built-in layouts are embedded resources and are read-only; "customize" copies one into the store.
- **Renderer** (app, `Text_Grab.Editing.Layout`): `LayoutRenderer` turns a `DockNode` tree into `Grid`s with `GridSplitter`s (D1) through the pane factories, and writes splitter sizes and collapsed state back to the live layout instance. In E4b it also turns a `ChromeDefinition` into a menu bar and the bottom-bar list into buttons, through `ActionCommand`.
- **Scope (D5):** each window holds its own live copy of a layout. Resolution order when a window opens: an explicit layout requested by the caller → the layout stored with the history item → the default for the file type → the global default (Classic).
- **History:** the layout ID is stored on `HistoryInfo` as an optional string, omitted when null. Not an enum: `EditorMode` is written by name, so 4.16 cannot read an entry saved as `Code`, and a layout ID must not repeat that.
- **Entry points:** `WindowUtilities` gains an optional layout ID when opening an Edit Text Window; `--layout <id>` on the command line; `text-grab://edit-text?layout=<id>` (parsed in `ProtocolUtilities`).

---

## E4a — Pane host and pane layouts (Opus)

Requires E3 `calc` merged. Does **not** require E2 or the other E3 clusters.

### Steps

1. **Capture a baseline before changing anything.** Using the automation host, dump the UI Automation subtree of the editor area and the calc pane (control type, name, `AutomationId`, enabled state, order) in Text, Markdown, Spreadsheet and Code modes, calc pane open and closed. Commit the dumps as test fixtures.
2. Model records + JSON round-trip tests in `Tests.Core`, including: unknown pane ID, unknown schema version, a file with no chrome members, a file *with* chrome members (they must survive a load/save cycle untouched even though E4a cannot render them).
3. `ILayoutStore` + `FileLayoutStore` + tests.
4. **Author `classic.json`:** the editor pane, and the calc pane to its right, pixel-sized and collapsed, matching today.
5. `LayoutRenderer` for the dock tree. The window's XAML keeps its menu bar, bottom bar, status strips and overlays; the renderer fills the region between them.
6. Put the rendered path behind a setting that defaults **off**. With it on, the window uses the renderer; off, the hand-written XAML.
7. **Parity test:** with the setting on, the dumps equal the step-1 fixtures. Fix differences until they match, or record each intentional difference in this file with a reason.
8. Migrate persisted state: on first use, the existing calc width and open state become the user's live Classic layout. The old settings keep being written so a downgrade still works.
9. Flip the setting's default to **on**. Leave the old XAML path in place for one release.
10. Entry points: `WindowUtilities` parameter, `--layout`, protocol parameter, the history field, with tests for the parsing and for an older-shaped history file.

### Out of scope

- Generating menus, the bottom bar or status items (E4b). A layout that carries chrome members is accepted and ignored.
- Any layout other than Classic (E5 adds them with their panes). A layout picker (E6).
- Drag-to-dock, floating panes, tab tear-off.

### Done when

The default window's dock area is rendered from `classic.json`, the parity test passes, existing users' calc pane state carries over, and a second layout file dropped in the store opens with `--layout`.

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
- Manual, setting on (E4a): the calc pane opens, resizes, and restores its width and open state from settings and from history; calc scrolling follows the editor in Text and Code modes; switching between all four modes leaves the panes alone; both themes; 100% and 200% DPI.
- Manual, setting on (E4b): every menu and submenu opens and is in the same order; every access key (`Alt` + letter) and shortcut works, **including from inside the code editor**, which has its own key bindings; tab order through the window is unchanged; bottom-bar customization from the settings page still applies; a screen reader announces the same names (Narrator spot check).
- Manual, setting off: the old path still works.

## Traps

- **Dynamic menus** are populated on `SubmenuOpened` and cleared on close to keep memory down. The renderer must keep that laziness; do not build every submenu up front.
- Access keys and `InputGestureText` are part of parity. So is the difference between a menu item that is disabled and one that is hidden. Some are **state-dependent**: the Code Language and Show Whitespace items apply only in Code mode, and Toggle Case's gesture text changes with the find bar.
- AvalonEdit handles keys before the window does. `CodeEditor` removes its Ctrl+D and Ctrl+I bindings so the window's Duplicate Line and Isolate Selection work; a generated shortcut that collides with another AvalonEdit binding will silently do the editor's thing instead. Test every generated gesture with focus in the code editor.
- `GridSplitter` behavior depends on whether neighbors are star or pixel sized; the calc pane uses a pixel width with a remembered last width. Model exactly that (`DockSizeUnit`), or resizing will feel different.
- A layout file is user-editable input. Unknown IDs, a missing editor pane, sizes of zero or negative, and absurd nesting depth must all render something usable rather than throw.
- If an owner confirms AvalonDock for D1 instead, stop: the renderer steps change substantially and this file needs revising first.
