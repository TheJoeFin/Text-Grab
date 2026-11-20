# E6 — Ribbon, layout picker and layout editor

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | Medium–large / Medium |
| Recommended model | Opus for `ribbon` and `editor`; Sonnet for `picker` and `defaults` |
| Depends on | `picker`: E4a. `defaults`: E4a and `picker`. `ribbon` and `editor`: E4b |
| Required decisions | D2 (ribbon) — must be `Confirmed` before `ribbon` goes beyond a spike |
| Branch | `v5/e6-<part>` |
| Ships in | `picker`: 4.17.0-beta2. The rest: 5.0.0-beta1. See [Release-Plan.md](Release-Plan.md). |

Read [README.md](README.md) first.

This phase has four parts. Your prompt names which. `picker` first; the others in any order.

## Goal

Let users choose, switch and build layouts without editing JSON, and offer a ribbon as an alternative to the menu bar.

## Current state (verify)

- E4a: `EtwLayoutDefinition`, `ILayoutStore`, `LayoutRenderer` for the dock tree (rendered through AvalonDock with pane chrome hidden, D1), built-in layouts as embedded resources. The menu bar, bottom bar and status bar are still hand-written XAML.
- E4b (needed by `ribbon` and `editor` only): chrome rendered from the definition; `ChromeKind.Ribbon` modeled but rendered as a menu bar.
- E5 may have added more built-in layouts and panes.
- `Text-Grab/Controls/BottomBarSettings.xaml(.cs)` is the existing "customize the bottom bar" dialog: two lists, add/remove/reorder. It is the starting point for the layout editor.
- `Text-Grab/Pages/EditTextWindowSettings.xaml(.cs)` is the Edit Text Window settings page.
- `Text-Grab/Utilities/SettingsImportExportUtilities.cs` handles settings export and import.

---

## `picker` — Layout switcher (Sonnet)

- A layout menu in the window (in the existing Window menu for Classic chrome) listing built-in and user layouts, with the current one checked. In 4.17 this is hand-written XAML with a dynamic list, like the window's other menus; E4b later expresses it as a `DynamicList` chrome item.
- A **Panes** submenu beside it: one checkable item per registered pane factory. Checking adds the pane to the live layout at its `DefaultDock`, or shows it if the layout already has it collapsed. Unchecking collapses a pane that is in the layout's saved definition, and removes and detaches one that is not. This is how a 4.17 user arranges panes without a layout editor; panes have no close buttons or title bars in 4.17 (D1), so this menu and the existing toggles are the only way.
- **Keep the calc controls in sync.** The Panes item for calc must agree with `ShowCalcPaneMenuItem`, `CalcToggleButton`, Ctrl+P, the `CalcShowPane` setting and the history-restore flag on `ShowCalcPaneMenuItem.Tag`.
- **The command palette** (Ctrl+Shift+P) lists menu items automatically, but it only sees dynamic submenus that `OpenCommandPaletteAsync` fills first. Fill the Layouts list there, as grab templates are, or it won't appear in the palette.
- Switching re-renders the dock area in place: the document, selection and file state are untouched because they live on the session, not in the layout. Panes not in the new layout are detached; new ones are attached. The editor pane is never rebuilt by a switch, so each editor's own undo history (the text box's, and AvalonEdit's in Code mode) survives. With AvalonDock (D1), keep the editor's `LayoutDocument` and its content across a switch, and rebuild only the panes around it.
- A layout's `DefaultMode` and `DefaultCodeLanguage` apply when a window **opens** with it. Switching layouts on a window that already holds a document changes the mode only if the new layout names one, and never when that would lose anything (a spreadsheet with table formatting stays a spreadsheet).
- "Save current layout as…", "Reset to built-in", "Delete" (user layouts only), "Set as default".
- Actions for each registered in the action registry (`textgrab.layout.switch` with the layout ID as config, and so on), so a layout can be bound to a hotkey or a button.
- Until E2 lands (4.18), these are ordinary click handlers; register their descriptors now so the IDs are pinned.
- Test: switch Classic → another layout → Classic with unsaved text and a selection, once in Text mode and once in Code mode; assert text, selection, dirty flag and (in Code mode) language and undo are unchanged.

### After the `picker` run (2026-11-20, `v5/e6-picker`)

**Decisions marked "orchestrator" were made by the agent that launched the run, after the run stopped on a conflict. The owner can override them.**

- **Built.** App: `EditTextWindow.LayoutPicker.cs`, `LayoutPickerActions` (`textgrab.layout.switch` with config `LayoutSwitchConfig(LayoutId)`, `.saveAs`, `.reset`, `.delete`, `.setDefault`; descriptors only, nothing in the registry runs them yet). Core: `LayoutDefaults.ModeOnSwitch` and `CodeLanguageOnSwitch`, `LayoutPaneEditor` (add or remove a pane at its default side, and `HasPaneChanges`), `LayoutIds.FromName`, `EtwLayoutDefinition.BasedOn`, `EditTextTableDocument.HasViewFormatting`. **Layouts** and **Panes** are submenus of the Window menu (`Lay_outs`, `Pane_s`: the other letters are taken), collapsed when the pane layout is off, so the top-level menu bar and both parity fixtures are unchanged. They fill when they open and in the command palette's `Task.WhenAll`. The Layouts list is built after the menu opens, so its items reach UI Automation by name only (their IDs are set but do not reach it); the Panes items are made once, when the window is initialized (a submenu with no items does not open), so their IDs do (`EditText.Menu.Panes.calc`, `.recent`, `.mermaid`, `.markdownPreview`), and selecting them by name fails because the item and its text child share it. The fixed Layouts items have IDs too (`EditText.Menu.Layouts.SaveAs` and so on). `WindowMenuItem` gained `EditText.Menu.Window`.
- **Mode on a switch (orchestrator).** E6 line 39 won over the earlier "never changes the mode". A switch to a layout that names a mode changes the mode, except that a spreadsheet whose table has wrapped cells, column widths or row heights stays a spreadsheet, and **Markdown mode is never left** (there is no cheap check that the rendered document gives back the same text). Entering Code mode sets the layout's `DefaultCodeLanguage` only if the window has no language yet (`isCodeLanguageSet`). A layout with no mode (Classic, Explorer) changes nothing, so going from Mermaid back to Classic stays in Code mode. A window's open-time rules (`LayoutDefaults` 1-3) are unchanged. E4's rule (4) now says the same.
- **Panes menu.** One checkable item per registered factory. Checking shows a pane the layout has collapsed, or adds one it lacks at its `DefaultDock` (a side of the editor; a vertical split is wrapped around the root for Top and Bottom). Unchecking collapses a pane that is in the layout as it was last switched to (`appliedLayout`), and removes and detaches one the menu added. The state shown comes from the live layout when the menu opens. Calc goes through the existing toggle path: with `persistCalc` the menu writes `CalcShowPane` and calls `SetCalcPaneVisibility`, as the toggle button and Ctrl+P do; tests use `SetCalcPaneOpen`. A calc pane the menu added to a layout without one leaves again when hidden. The window's open state and width for calc still carry across switches, as before.
- **Save, reset, delete, default.**
  - **Save as** asks for a name (a `MessageBox` with a `TextBox`, `EditText.LayoutName`), makes the ID with `LayoutIds.FromName` (never a built-in's or an earlier layout's: `-2`, `-3`), saves the live layout (panes, sizes, shown state) with `basedOn` = the built-in it came from (kept through copies of copies), and the window then shows the saved layout.
  - **Reset to built-in** (orchestrator): a built-in layout with pane changes returns to its definition; a saved layout returns to its `basedOn` built-in. It is disabled with nothing to return to. It never edits a user file, and the calc pane's shown state is the window's, so a reset does not hide it (and `HasPaneChanges` ignores it).
  - **Delete** is for the saved layout the window shows, after a confirmation. The window keeps its live copy (orchestrator), so the list then has no checked item; other windows keep theirs. Deleting the default clears the default.
  - **Set as default** (orchestrator) is a checkable item, in `layouts/.store.json` (`{ "globalDefault": "<id>" }`; the leading dot can never start an ID, and the listing skips it). `FileLayoutStore` keeps members it does not know, for E6 `defaults`, which should add its per-file-kind entries to the same file. Resolution is now: caller, history item, file type, **this default**, Classic. Setting Classic clears it. A history item saved in Classic while another layout is the default stores `classic`, not null, or it would reopen in the default; items saved earlier as null do reopen in it.
- **Store layouts are rendered first (orchestrator).** `ILayoutStore` gained synchronous `Load` and `ReadDefaults` (one small file each, read at window construction, only with the pane layout on), so the window renders the layout it ends in, saved or built in, and no window renders Classic first and builds a calc pane to detach. A saved layout without calc never makes the calc pane (`CalcPane.CreatedCount`; test). This closes the gap E4a described. A missing or malformed default, or one naming a layout that is gone, opens Classic and says why.
- **Test seams.** `EditTextWindow.LayoutStoreOverride` (the test host points it at a temporary folder for the whole assembly, so no test reads the person's layouts or default), `LayoutNamePromptOverride`, `DeleteLayoutConfirmOverride`. Picker tests run in their own non-parallel collection.
- **The editor across a switch.** `LayoutRenderer.Render` still makes a new `LayoutDocument` each time and moves the same `ReadingEditorHost` into it. The round-trip tests (Text mode on a file with an unsaved edit and a selection; Code mode with a language and an edit) pass unchanged: text, selection, dirty flag, language and undo survive, so `Render` was left alone.
- **UI test flake, not the picker's.** `pane-layout-editor-area-matches-the-hand-written-fixtures` failed on `spreadsheet.calc-open` in every EditText run of this branch (one missing `PART_TopHeaderGripper` on row 12, a row at the bottom of the viewport). Running the exporter against a build of the parent commit (`89a51c2`) passed once and failed once, so it is the same flake as before, now more frequent on this machine; no fixture was changed. Worth a look by whoever owns the parity test (the row near the viewport edge is clipped or not).
- **Not done.** No hotkey or button binding for the layout actions (E2). No per-file-kind defaults or import/export (`defaults`). The Panes menu does not show a pane inside a tab group other than the selected tab. Checked by hand: nothing. Still for the owner: both themes, keyboard-only use of the two submenus, touch, a screen reader on the dynamic items.

## `defaults` — Per-file-type defaults and import/export (Sonnet)

- A section on the Edit Text Window settings page: global default layout, and a default per file kind (Markdown, spreadsheet/CSV, Mermaid, audio/video, code, plain text). "Code" is one kind covering every extension in `CodeLanguages`; do not offer a default per language. Stored in the layout store's metadata file, not in `Settings.settings`.
- Resolution order is E4's: explicit → history item → file type → global.
- The file-type step already has a built-in map from 4.17 ([E4a](E4-Layout-Engine.md) step 11: `.mmd` and `.mermaid` → Mermaid). A user's default for a file kind overrides the built-in entry, and resetting it returns to the built-in entry. Do not copy the built-in map into the metadata file: keep it as the fallback, so built-in entries added later still reach users who never changed a default.
- Export a layout to a file; import one (validate, show its name and what panes it uses, save as a user layout). Unknown pane or action IDs import with a warning, not a failure.
- Include user layouts in `SettingsImportExportUtilities` export and import. Read how it handles the other file-backed settings and follow that.

## `ribbon` — Ribbon chrome (Opus)

Per D2: a tabbed command bar built from WPF-UI controls, generated from `ChromeDefinition`.

1. **Spike first**, not merged: one tab with three groups rendered from a `ChromeDefinition`, in both themes, at 100% and 200% DPI, at a narrow window width. Report what you built and what it cost before continuing. If D2 is still `Default`, stop here and ask.
2. Mapping: `ChromeGroup` at the top level → a tab; a nested group → a labelled group of buttons; `Action` → a button with icon and label from the `ActionDescriptor`; `Submenu` → a drop-down button; `DynamicList` → a drop-down populated on open; toggles reflect checked state.
3. Overflow: when the window is too narrow, groups collapse to a drop-down, lowest priority first. Add an optional `Priority` to `ChromeGroup` if needed (schema version bump with a migration).
4. Keyboard: `Alt` shows key tips; arrow keys move within and between groups; every button is reachable.
5. A built-in **Ribbon** layout: Classic's panes with ribbon chrome, grouping the same actions as the Classic menus.
6. Collapsed/expanded state persists in the layout.

Mode-specific items: Code Language and Show Whitespace apply only in Code mode, as the spreadsheet items apply only in Spreadsheet mode. Disable them outside their mode rather than adding contextual tabs (out of scope below).

Traps: icon-only buttons need tooltips and accessible names. A ribbon taller than the menu bar reduces editing space on small screens — support a single-row collapsed mode. Do not introduce a second theming system.

## `editor` — Layout editor (Opus)

A dialog (successor to `BottomBarSettings`) for editing a user layout without JSON.

- **Chrome tab:** choose menu bar / ribbon / none; a tree of groups and items; add an action from a searchable list of the registry; reorder, rename, remove; add separators and submenus.
- **Panes tab:** the dock tree as a nested list; add a pane from the registered factories; choose side and size; remove; reorder. (Dragging panes is off through 4.17 under D1. If AvalonDock's drag-to-dock is on by 5.0, the tree editor stays the keyboard-accessible way to arrange panes.)
- **Bottom bar tab:** today's `BottomBarSettings` behavior, driven by action IDs.
- Live preview: apply changes to the open window as they are made; **Cancel** restores the layout as it was when the dialog opened.
- Editing a built-in layout creates a user copy on first change.
- Validation before save: an editor pane must exist; no empty tab groups; sizes are positive.

Test the editor's view model exhaustively (add, move, remove at every level; cancel restores; built-in becomes a copy); keep the view thin.

---

## Out of scope

- Turning on AvalonDock's drag-to-dock, floating windows or auto-hide; each is its own D1 follow-up. Multi-document tabs in one window.
- A full Office-style ribbon with galleries, contextual tabs and quick-access toolbar customization.
- Sharing layouts online.

## Verification

- All three test executables pass.
- Manual, per part: the flows listed above, both themes, keyboard only, and that Classic remains the default and is unchanged for a user who never opens any of this.
- Report any new package for the MSIX check.

## Done when

A user can switch layouts from the window, set a default per file type, build a layout in the editor, and turn on a ribbon — all without touching a file.

| Part | Status |
|---|---|
| `picker` | Done (see the commit that adds this line) |
| `defaults` | Not started |
| `ribbon` | Not started |
| `editor` | Not started |
