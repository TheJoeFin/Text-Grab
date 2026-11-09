# E4 — Layout engine and the Classic layout

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | Large / **Highest in the track** (parity with the hand-written window) |
| Recommended model | Opus |
| Depends on | E3 |
| Required decisions | D1 (docking), D5 (layout scope) |
| Unblocks | E5, E6, W3 |
| Branch | `v5/e4-layout-engine` |

Read [README.md](README.md) first.

## Goal

The Edit Text Window builds itself from a layout definition. Ship exactly one layout, **Classic**, that reproduces today's window. From the user's side nothing changes; from the code's side, a "window type" is now a JSON file.

## Current state (verify)

- After E3, `EditTextWindow` is a shell with hand-written XAML that places: title bar (`UiTitleBar`), menu bar, the editor area, the calc pane with its splitter, the bottom bar (`BottomBarScrollViewer` / `BottomBar`), status strips, overlays (`LoadingStack`, `PendingFileCloseDialogHost`).
- Panes implement `IEditorPane` and have DI-registered factories (E3). Actions are invoked by ID through `EditorActionInvoker` and bind through `ActionCommand` (E2).
- Persisted window state today: position and size ("restore position"), `CalcPaneWidth`, calc pane open, wrap, font, margins, always on top, bottom bar hidden, bottom-bar button list, editor mode per history item.
- UI test infrastructure: `UiTests/TextGrab.AutomationHost`, `.github/workflows/interactive-ui.yml`, `Tests/UiAutomationContractTests.cs`, and `AutomationId`s such as `EditText.Menu.Selection`, `EditText.MarkdownEditor`.

## Design

**Model** in `Text-Grab.Core`, namespace `Text_Grab.Core.Layouts` — records, `System.Text.Json`, versioned, no WPF:

```csharp
public sealed record EtwLayoutDefinition(string SchemaVersion, string Id, string Name,
    ChromeDefinition Chrome, DockNode Root, IReadOnlyList<string> BottomBarActionIds,
    IReadOnlyList<string> StatusItemIds, EtwEditorMode DefaultMode, WindowOptions Window);

public enum ChromeKind { MenuBar, Ribbon, None }
public sealed record ChromeDefinition(ChromeKind Kind, IReadOnlyList<ChromeGroup> Groups);
public sealed record ChromeGroup(string Title, string? AccessKey, IReadOnlyList<ChromeItem> Items);
public sealed record ChromeItem(ChromeItemKind Kind /*Action|Separator|Submenu|DynamicList*/,
    string? ActionId, string? Title, string? DynamicSourceId, IReadOnlyList<ChromeItem>? Children);

[JsonPolymorphic] public abstract record DockNode;
public sealed record SplitNode(DockOrientation Orientation, IReadOnlyList<DockChild> Children) : DockNode;
public sealed record DockChild(DockNode Node, double Size, DockSizeUnit Unit /*Star|Pixel*/, bool Collapsed);
public sealed record PaneNode(string PaneId, JsonElement? Config) : DockNode;   // "textgrab.pane.editor" is the editor area
public sealed record TabGroupNode(IReadOnlyList<PaneNode> Tabs, int SelectedIndex) : DockNode;

public sealed record WindowOptions(bool AlwaysOnTop, bool RestorePosition, bool ShowBottomBar);
```

- `DynamicList` chrome items cover menus filled at open time (recent items, languages, patterns, templates, web searchers). Each has a `DynamicSourceId` resolved app-side to the existing population code.
- **Store:** `ILayoutStore` in Core; `FileLayoutStore` in Core.Windows writing `layouts/<id>.json`, the same way W1's workflow store does (reuse its approach or, if W1 has not landed, `FileUtilities`' packaged/unpackaged storage). Built-in layouts are embedded resources and are read-only; "customize" copies one into the store.
- **Renderer** (app, `Text_Grab.Editing.Layout`): `LayoutRenderer` turns a `DockNode` tree into `Grid`s with `GridSplitter`s (D1), a `ChromeDefinition` into a menu bar, and the bottom-bar list into buttons, all through `ActionCommand` and the pane factories. The renderer writes splitter sizes and collapsed state back to the live layout instance.
- **Scope (D5):** each window holds its own live copy of a layout. Resolution order when a window opens: an explicit layout requested by the caller → the layout stored with the history item → the default for the file type → the global default (Classic).
- **Entry points:** `WindowUtilities` gains an optional layout ID when opening an Edit Text Window; `--layout <id>` on the command line; `text-grab://edit-text?layout=<id>` (parsed in `ProtocolUtilities`).

## Steps

1. **Capture the baseline before changing anything.** Using the automation host, dump the Edit Text Window's UI Automation tree (control type, name, `AutomationId`, access key, enabled state, order) in Text, Markdown and Spreadsheet modes, calc pane open and closed. Commit the dumps as test fixtures. Also dump the full menu tree with shortcut text.
2. Model records + JSON round-trip tests in `Tests.Core`, including unknown pane ID, unknown action ID and unknown schema version (load succeeds; rendering shows a placeholder and logs; nothing is deleted).
3. `ILayoutStore` + `FileLayoutStore` + tests.
4. **Author `classic.json`** from the current XAML: every menu, submenu, separator and dynamic list, in order, with access keys.
5. `LayoutRenderer`: dock tree first (editor + calc), then chrome, then bottom bar and status items.
6. Put the rendered path behind a setting that defaults **off**. With it on, the window uses the renderer; off, the hand-written XAML.
7. **Parity test:** with the setting on, the UI Automation dumps equal the step-1 fixtures. Fix differences until they match, or record each intentional difference in this file with a reason.
8. Migrate persisted state: on first use, the existing calc width / open state / bottom-bar buttons / window options become the user's live Classic layout. The old settings keep being written so a downgrade still works.
9. Flip the setting's default to **on**. Leave the old XAML path in place for one release; add a note in this file saying when it can be deleted.
10. Entry points: `WindowUtilities` parameter, `--layout`, protocol parameter, with tests for the parsing.

## Out of scope

- Any layout other than Classic (E5 adds them with their panes).
- Ribbon chrome, a layout picker, a layout editor (E6). `ChromeKind.Ribbon` exists in the model but renders as a menu bar for now.
- Drag-to-dock, floating panes, tab tear-off.

## Verification

- All three test executables pass, including the parity test.
- Manual, setting on: every menu and submenu opens and is in the same order; every access key (`Alt` + letter) and shortcut works; tab order through the window is unchanged; the calc pane opens, resizes and restores its width from history; bottom-bar customization from the settings page still applies; both themes; 100% and 200% DPI; a screen reader announces the same names (Narrator spot check).
- Manual, setting off: the old path still works.

## Done when

The default Edit Text Window is rendered from `classic.json`, the parity test passes, existing users' persisted window state carries over, and a second layout file dropped in the store can be opened with `--layout`.

## Traps

- **Dynamic menus** are populated on `SubmenuOpened` and cleared on close to keep memory down. The renderer must keep that laziness; do not build every submenu up front.
- Access keys and `InputGestureText` are part of parity. So is the difference between a menu item that is disabled and one that is hidden.
- `GridSplitter` behavior depends on whether neighbors are star or pixel sized; the calc pane uses a pixel width with a remembered last width. Model exactly that (`DockSizeUnit`), or resizing will feel different.
- A layout file is user-editable input. Unknown IDs, a missing editor pane, sizes of zero or negative, and absurd nesting depth must all render something usable rather than throw.
- If an owner confirms AvalonDock for D1 instead, stop: steps 5–7 change substantially and this file needs revising first.
