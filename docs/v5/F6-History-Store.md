# F6 — Observable history store (optional)

| | |
|---|---|
| Track | Foundation |
| Size / risk | Small–medium / Medium (user data) |
| Recommended model | Sonnet |
| Depends on | — |
| Unblocks | Live updates in the recent-items pane (E5); a "save to history" workflow node |
| Branch | `v5/f6-history-store` |

Read [README.md](README.md) first, then `../Core-Split-Plan.md` §4.6 (batch 6e).

**Only do this phase when a consumer needs it.** The recent-items pane can ship without it by reloading on activation.

## Goal

Move the in-memory history lists and the debounced write out of the app-side `HistoryService` into a headless `HistoryStore` that raises change notifications, so non-WPF code can read history and observe changes.

## Current state (verify)

- `Text-Grab.Core.Windows/Utilities/HistoryFileUtilities.cs` (static, ~500 lines): load, recover, write, normalize, word-border sidecars, retention caps, artifact deletion.
- `Text-Grab.Core.Windows/Models/HistoryInfo.cs`: the item model.
- `Text-Grab/Services/HistoryService.cs` (~700 lines, app-side): the two `List<HistoryInfo>` fields (text-only and with-image), two `DispatcherTimer`s (debounced save, idle cache release), the cached fullscreen `Bitmap` and its HBITMAP, recent-grab `MenuItem` building, `SaveToHistory(GrabFrame)` / `SaveToHistory(EditTextWindow)`, `RecordRecentFile`.
- 52 call sites use `Singleton<HistoryService>.Instance`.
- `Text-Grab/Services/TrayFlyoutItemProvider.cs` projects history into flat, searchable items.
- `Tests/HistoryServiceTests.cs` covers the service.

## Design

`HistoryStore` in `Text-Grab.Core.Windows` (namespace `Text_Grab.Services`):

- Owns both lists, loaded lazily through `HistoryFileUtilities`.
- `IReadOnlyList<HistoryInfo> TextHistory`, `ImageHistory`; `Add`, `Remove`, `Clear` methods mirroring what `HistoryService` does to the lists today.
- `event EventHandler<HistoryChangedEventArgs> Changed`.
- Debounced write with `System.Threading.Timer` (interval identical to today's `DispatcherTimer`), plus `FlushAsync()` for shutdown.
- Thread-safe: a single lock around list mutation; events raised outside the lock.

`HistoryService` keeps its name, its public API and everything WPF: it holds a `HistoryStore`, forwards list operations to it, and keeps the cached bitmap, the idle-release timer, `MenuItem` building and the window-typed `SaveToHistory` overloads.

## Steps

1. **Round-trip tests first** in `Tests.Core.Windows` against `HistoryFileUtilities` with a temp folder: write N items, reload, compare; exceed each retention cap; corrupt file → recovery path. They pass before any change.
2. Read `HistoryService` end to end and list which private state each public method touches. Put the list in your report.
3. Introduce `HistoryStore` with tests (add, remove, cap enforcement, `Changed` raised once per mutation, debounce coalesces writes, `FlushAsync` writes immediately).
4. Rewire `HistoryService` onto the store. Public signatures unchanged; no call-site edits.
5. Make sure the app flushes the store on shutdown wherever `HistoryService` writes on shutdown today.

## Out of scope

- Renaming `HistoryService` or touching its 52 call sites.
- Changing file formats, file names, caps or dedup rules.
- The pane itself (E5).

## Verification

- All three test executables pass, `HistoryServiceTests` unchanged.
- Manual: make a fullscreen grab, an Edit Text Window save-to-history and a Grab Frame grab; restart the app; all three appear in the tray flyout and in the Recent menus; "Previous region grab" still works.

## Done when

A `Tests.Core.Windows` test can add an item to a `HistoryStore`, observe `Changed`, flush, and reload it from disk.

## Traps

- This is user data. If anything about load or recovery looks ambiguous, stop and ask rather than guess.
- `DispatcherTimer` ticks on the UI thread; `System.Threading.Timer` does not. Anything the save callback touched that assumed the UI thread must move behind the lock or be marshalled.
- The cached fullscreen bitmap's lifetime is tied to the idle timer, not to the save timer. Keep the two separate.
- Writes at shutdown must complete; a background timer thread is killed when the process exits.
