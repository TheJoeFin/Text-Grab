# E2 — Edit Text Window commands onto the action registry

| | |
|---|---|
| Track | Edit Text Window |
| Size / risk | Large / Medium (users' saved buttons must survive) |
| Recommended model | Opus for the first run (foundation + first cluster); Sonnet for each remaining cluster |
| Depends on | A1, E1 |
| Unblocks | E3 |
| Branch | `v5/e2-commands-<cluster>` |

Read [README.md](README.md) first, then the inventory appendix in [A1-Action-Registry.md](A1-Action-Registry.md).

This phase is done as several runs: one **foundation** run, then one run per **cluster**. Your prompt names which.

## Goal

Every action in the Edit Text Window is a registered action with a stable ID. Menus, the bottom bar, the context menu, keyboard shortcuts and post-grab actions all invoke actions by ID. The reflection that binds button names to window methods is deleted.

## Current state (verify)

- 17 static `RoutedCommand` fields at the top of `EditTextWindow.xaml.cs`; `GetRoutedCommands()` returns them by name; `SetupRoutedCommands` wires bindings and gestures.
- About 200 `*_Click` handlers referenced from 205 `MenuItem`s in `EditTextWindow.xaml`.
- `Text-Grab/Models/ButtonInfo.cs`: `AllButtons` (about 100 entries), each with `ButtonText`, `SymbolIcon`, and either `Command` (a `RoutedCommand` name) or `ClickEvent` (a window method name).
- `Text-Grab/Utilities/CustomBottomBarUtilities.cs`: `GetBottomBarButtons(EditTextWindow)` resolves `ClickEvent` by reflection over the window's methods and `Command` through `GetRoutedCommands()`.
- Users' customized bottom bars are persisted as serialized `ButtonInfo` lists (see `GetCustomBottomBarItemsSetting` / `SaveCustomBottomBarItemsSetting` and `SettingsService`).
- `Text-Grab/Utilities/PostGrabActionManager.cs` executes `ButtonInfo`s against text after a grab; users' enabled post-grab actions are persisted too.
- `Tests/EditTextWindowActionCatalogTests.cs` asserts every `ButtonInfo` name resolves to a command or method.

## Design

In `Text-Grab.Core`, namespace `Text_Grab.Core.Actions` (extends A1):

```csharp
public interface IEditorCommand
{
    ActionDescriptor Descriptor { get; }
    bool CanExecute(IEditorSession session);
    Task ExecuteAsync(IEditorSession session, CancellationToken ct);
}
```

- A **text transform** (`ITextTransform`, from A1) needs no editor command of its own. One generic adapter runs any transform against `session.GetSelectedTextOrAllText()` / `ReplaceSelectedTextOrAllText`, honoring the spreadsheet per-cell behavior that `TryApplySpreadsheetTextTransform` implements today.
- An **editor command** is for actions that need selection, caret, mode or file state (split on selection, isolate selection, move line up, select word, save).
- Commands that need app services or open windows (find and replace, launch fullscreen grab, settings) are implemented app-side and registered from the app.
- **`EditorActionInvoker`** (app): `Task InvokeAsync(string actionId, IEditorSession session)` and `bool CanInvoke(...)`. Every surface calls this.
- **WPF binding:** one `ActionCommand : ICommand` class, constructed with an action ID and a session, so XAML and generated buttons bind normally. Keyboard gestures come from `ActionDescriptor.DefaultGesture`.
- **`ButtonInfo` gains `ActionId`.** A static migration table maps every legacy `Command` and `ClickEvent` string to an action ID. On load, a `ButtonInfo` with no `ActionId` is upgraded through the table; on save, `ActionId` is written and the legacy fields are kept so a downgrade to 4.x still works.

## Foundation run (Opus)

1. Add `IEditorCommand`, the transform-to-session adapter, `EditorActionInvoker`, `ActionCommand`.
2. Add `ButtonInfo.ActionId` and the migration table, initially mapping only the entries the first cluster covers. Unmapped entries keep using the old reflection path — both paths coexist until the last cluster.
3. Rewrite `EditTextWindowActionCatalogTests`: every entry in `ButtonInfo.AllButtons` resolves through *either* the migration table to a registered ID *or* the legacy path. Add a second test that fails if a legacy string is mapped to an unregistered ID.
4. Add a fixture test with a serialized `ButtonInfo` list in the 4.16 format (capture a real one from the settings format; do not invent the shape) and assert it loads to the same buttons.
5. Convert the **Lines/cleanup cluster** end to end as the pattern for later runs: trim each line, single line, remove duplicate lines, shuffle lines, correct GUIDs, toggle case, replace reserved characters, unstack, unstack group, try-to-number, try-to-alpha, clean up text. For each: menu item binds to `ActionCommand`; the old handler and `RoutedCommand` are deleted; the migration table gains its row.
6. Write a "How to convert a cluster" section at the bottom of this file from what you learned, for the Sonnet runs.

## Cluster runs (Sonnet), in this order

| Cluster | Covers |
|---|---|
| `selection` | split on / after selection, isolate, delete all of selection, delete by pattern, insert on every line, select word / line / all / none, move line up / down, duplicate line, add line above |
| `search` | web search family, default web search, find and replace, similar matches, regex pattern, QR code, launch URI |
| `templates` | apply pattern, apply pattern per line, apply grab template, per line |
| `files` | new, open, save, save as, close, recent, new window with selection |
| `capture` | fullscreen grab, grab frame, previous region, camera, OCR paste, folder OCR, language |
| `ai` | summarize, meeting notes, rewrite, convert table, translate family |
| `transcription` | transcribe audio file, live transcription toggle, source, model |
| `spreadsheet` | every spreadsheet menu and context-menu action |
| `view` | mode switches, wrap, margins, always on top, calc pane toggle, hide bottom bar, font, spell check |
| `final` | remove the reflection path from `CustomBottomBarUtilities`, delete `GetRoutedCommands()`, make the catalog test require a mapping for every entry, point `PostGrabActionManager` at the invoker |

Each cluster run: follow "How to convert a cluster"; one commit per action or small group; all gates after each commit.

## Out of scope

- Generating menus from data (E4). The XAML menu structure stays; only what each item invokes changes.
- New actions, renamed actions, changed shortcuts, changed menu text.
- Hotkeys and tray actions (already routed in F5).

## Verification

- All three test executables pass after every commit.
- Manual, per cluster: every converted action from its menu item, its keyboard shortcut and a bottom-bar button, in Text mode and — where it applies today — in Spreadsheet and Markdown modes.
- Manual, after `final`: with a settings profile whose bottom bar was customized under 4.16, every button appears and works. Enabled post-grab actions still run after a fullscreen grab.

## Done when

`final` has landed: no `RoutedCommand` field and no reflection over `EditTextWindow` remain, and every `ButtonInfo` carries an `ActionId`.

## Traps

- **Losing a user's button is the failure that matters.** An unmapped legacy name must never be dropped silently: keep it, log it, and show it disabled rather than removing it.
- `CanExecute` for the paste/OCR-paste command uses a cached clipboard state (`_cachedClipboardHasOcrContent`) because querying the clipboard on every `CanExecute` was slow. Keep the cache.
- Some handlers read `sender` (which menu item, its `Tag`, its `IsChecked`). Those become action config or separate action IDs — decide per case and record it in the A1 inventory.
- Async handlers are `async void` today and swallow nothing. The invoker must surface exceptions the same way the UI does now (check what the window does on failure) rather than leaving unobserved tasks.
- Action IDs added here join A1's pinned-ID test.
