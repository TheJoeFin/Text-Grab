> **Draft for the owner to review. Not yet published.**

### Going back to 4.16

If you go back to Text Grab 4.16.0 or earlier after trying this beta:

- **Edit Text Window history saved in Code mode is deleted.** 4.16 doesn't know Code mode, so it skips those entries when it loads your history, then saves the history file without them. They won't come back if you update again. Your other history entries are kept.
- **Tesseract needs its own install again.** 4.16 doesn't include Tesseract, so Tesseract OCR only works there if Tesseract is installed separately.

Layouts don't add to this. The layout a history entry was saved with is stored as an optional text value that older versions ignore, so going back because of layouts loses nothing.

The portable version keeps history in the `history` folder next to `Text-Grab.exe`, so this only affects you if you run 4.16 from the same folder. To keep a copy either way, go to **Settings → Danger → Export Settings** and check **Include history (images and text)** before going back.

## Panes and layouts

The Edit Text Window can now show more than the editor and the calculations pane. A layout is a set of panes arranged around the editor.

- **Window menu → Layouts** switches layout. **Window menu → Panes** shows or hides one pane.
- Built-in layouts:
  - **Classic**: the editor and the calculations pane, as in 4.16.
  - **Explorer**: a Recent items pane beside the editor. It lists your recent grabs, history and files; double-click or press Enter to open one, Ctrl+Enter to open it in a new window. Closes #592.
  - **Mermaid**: source on one side, the live diagram on the other. `.mmd` and `.mermaid` files open in it, in Code mode.
  - **Markdown preview**: Markdown source in Code mode on one side, the rendered page on the other. `.md` files still open in Markdown mode as before; pick this layout to get the preview.
- **Save current layout as…** saves the panes you have open under a name you choose. **Reset** returns a built-in layout, or a saved layout's built-in starting point, to its original panes. **Delete** removes a saved layout. **Set as default** makes the layout new windows open in.
- **From the command line:** `--layout <id>` opens a window in that layout, for example `--layout markdown-preview`. On its own it opens an Edit Text Window; with a file it applies to that file's window. The `text-grab://edit-text` link takes the same thing as `?layout=<id>`.
- Switching layout changes the editor mode only when nothing would be lost. A spreadsheet with wrapped cells, column widths or row heights stays a spreadsheet, and Markdown mode is never left.

## Calculations pane

- In Code mode the results now scroll with the code editor, and the mouse wheel over them scrolls the editor. Before, they stayed put.

## What looks different from 4.16

If you never open the Layouts or Panes menus, the window should look like 4.16 apart from these:

- Double-clicking the splitter next to the calculations pane no longer toggles the pane. Use the toggle button, Ctrl+P, or the Panes menu.
- The resize bar now sits beside the calculations pane instead of over its edge, so at the same pane width the editor is 5 px narrower.
- When the calculations pane is hidden, its Copy All and Info buttons are no longer invisible Tab stops.

## If something breaks

Go to **Settings → Edit Text → Use the new pane layout** and turn it off. Windows you open after that use the 4.16 editor area. The setting is stored as `EtwPaneLayout` (`On` or `Off`; empty means the build's default, which is on). The old path is kept for this release only.

## Also in this beta

Changes made after beta1 that aren't about panes:

- **Command palette** in the Edit Text Window: press Ctrl+Shift+P and search the editor's commands by name.
- **Speech commands** in the Edit Text Selection menu (#712).
- **Markdown:** a formatting toolbar; table columns size to their content and refit when the window resizes; a button in the right gutter opens the table under the pointer in a new spreadsheet window; task-list checkboxes line up with their wrapped labels.
- **Spell check** in the Format menu, for the Edit Text Window only.
- **Copied file paths:** the global shortcut opens a copied file path directly, and the tray flyout offers to open it as a file or as plain text.
- **File associations:** Text Grab now appears in Open with and Default apps for 93 text file types (Store build) and the standalone build registers its own handlers, with icons. Image and Grab Frame associations are unchanged, and no default is set for you.
- **Reading position:** a temporary bookmark appears on the scrollbar.
- **History:** fixed updating existing Edit Text history items.
- **Reduced motion:** groundwork for following the Windows animation setting. Nothing changes yet.
- **Third-party notices** are up to date: the Licenses window now lists the packages that were missing and current versions, and `BUILT-WITH.md` matches.

## Known limits

- In Code mode the editor still wraps long lines while the calculations pane is open, so wrapped lines (and collapsed folds) push the results out of line with their source.
- The Mermaid pane draws the whole document, or in Markdown mode the first Mermaid block. It does not yet follow the caret to other blocks. Panning is by mouse only.
- Markdown preview scroll sync is approximate: it follows the editor by proportion, one way, so the two drift where headings, images or diagrams are taller than their source.
- The Panes menu doesn't show a pane that sits in a tab group other than the selected tab.

## Please report

Pane, layout, focus, theme (light, dark, switching while open) and DPI or multi-monitor problems especially, and anything in the Edit Text Window that behaves differently from 4.16. Open an issue at https://github.com/TheJoeFin/Text-Grab/issues/new and say which layout you were in and whether **Use the new pane layout** was on.

## What's Changed
* _To be filled in from the pull requests when the release is drafted._

**Full Changelog**: https://github.com/TheJoeFin/Text-Grab/compare/v4.17.0-beta1...v4.17.0-beta2
