# E6 — Ribbon, layout picker and layout editor

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | Medium–large / Medium |
| Recommended model | Opus for `ribbon` and `editor`; Sonnet for `picker` and `defaults` |
| Depends on | E4 |
| Required decisions | D2 (ribbon) — must be `Confirmed` before `ribbon` goes beyond a spike |
| Branch | `v5/e6-<part>` |

Read [README.md](README.md) first.

This phase has four parts. Your prompt names which. `picker` first; the others in any order.

## Goal

Let users choose, switch and build layouts without editing JSON, and offer a ribbon as an alternative to the menu bar.

## Current state (verify)

- E4: `EtwLayoutDefinition`, `ILayoutStore`, `LayoutRenderer`, built-in layouts as embedded resources, `ChromeKind.Ribbon` modeled but rendered as a menu bar.
- E5 may have added more built-in layouts and panes.
- `Text-Grab/Controls/BottomBarSettings.xaml(.cs)` is the existing "customize the bottom bar" dialog: two lists, add/remove/reorder. It is the starting point for the layout editor.
- `Text-Grab/Pages/EditTextWindowSettings.xaml(.cs)` is the Edit Text Window settings page.
- `Text-Grab/Utilities/SettingsImportExportUtilities.cs` handles settings export and import.

---

## `picker` — Layout switcher (Sonnet)

- A layout menu in the window (in the existing Window menu for Classic chrome) listing built-in and user layouts, with the current one checked.
- Switching re-renders the window in place: the document, selection, undo history and file state are untouched because they live on the session, not in the layout. Panes not in the new layout are detached; new ones are attached.
- "Save current layout as…", "Reset to built-in", "Delete" (user layouts only), "Set as default".
- Actions for each registered in the action registry (`textgrab.layout.switch` with the layout ID as config, and so on), so a layout can be bound to a hotkey or a button.
- Test: switch Classic → another layout → Classic with unsaved text and a selection; assert text, selection and dirty flag are unchanged.

## `defaults` — Per-file-type defaults and import/export (Sonnet)

- A section on the Edit Text Window settings page: global default layout, and a default per file kind (Markdown, spreadsheet/CSV, Mermaid, audio/video, plain text). Stored in the layout store's metadata file, not in `Settings.settings`.
- Resolution order is E4's: explicit → history item → file type → global.
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

Traps: icon-only buttons need tooltips and accessible names. A ribbon taller than the menu bar reduces editing space on small screens — support a single-row collapsed mode. Do not introduce a second theming system.

## `editor` — Layout editor (Opus)

A dialog (successor to `BottomBarSettings`) for editing a user layout without JSON.

- **Chrome tab:** choose menu bar / ribbon / none; a tree of groups and items; add an action from a searchable list of the registry; reorder, rename, remove; add separators and submenus.
- **Panes tab:** the dock tree as a nested list; add a pane from the registered factories; choose side and size; remove; reorder. (No drag-to-dock, per D1 — the tree editor is the mechanism.)
- **Bottom bar tab:** today's `BottomBarSettings` behavior, driven by action IDs.
- Live preview: apply changes to the open window as they are made; **Cancel** restores the layout as it was when the dialog opened.
- Editing a built-in layout creates a user copy on first change.
- Validation before save: an editor pane must exist; no empty tab groups; sizes are positive.

Test the editor's view model exhaustively (add, move, remove at every level; cancel restores; built-in becomes a copy); keep the view thin.

---

## Out of scope

- Drag-to-dock, floating windows, multi-document tabs in one window.
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
| `picker` | Not started |
| `defaults` | Not started |
| `ribbon` | Not started |
| `editor` | Not started |
