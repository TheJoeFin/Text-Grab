# F5 — Trigger infrastructure

| | |
|---|---|
| Track | Foundation |
| Size / risk | Medium / Medium (clipboard feedback loop) |
| Recommended model | Sonnet |
| Depends on | F1 |
| Unblocks | W1 (trigger nodes), the folder-tree pane in E5 |
| Branch | `v5/f5-triggers` |

Read [README.md](README.md) first.

## Goal

Three reusable event sources — clipboard changed, folder changed, hotkey pressed — behind one interface, each independently testable, none of them knowing about workflows.

## Current state (verify)

- **Clipboard**: the Edit Text Window subscribes to WinRT `Clipboard.ContentChanged` per window (`EditTextWindow.xaml.cs` ~line 6396, handler `Clipboard_ContentChanged` ~line 2846), guarded by an `IsAccessingClipboard` flag and the `ClipboardWatcherMenuItem` toggle. A Win32 `AddClipboardFormatListener` import exists unused in `Text-Grab.Core.Windows/NativeMethods.cs`.
- **Folder**: no `FileSystemWatcher` anywhere.
- **Hotkeys**: `NotifyIconUtilities.HotKeyManager_HotKeyPressed` (~line 86) is a `switch` over `ShortcutKeyActions` with 11 cases, each calling a static on `Text-Grab/Utilities/TrayActions.cs` via `RunOnUiThread`. `HotKeyManager` (a background message-loop form) does the registration and works; leave it.

## Design

In `Text-Grab.Core`, namespace `Text_Grab.Core.Abstractions`:

```csharp
public enum TriggerKind { Manual, ClipboardChanged, FileCreated, FileChanged, Hotkey }
public sealed record TriggerPayload(TriggerKind Kind, string? Text = null, string? FilePath = null);

public interface ITriggerSource : IDisposable
{
    event EventHandler<TriggerPayload> Triggered;
    void Start();
    void Stop();
}
```

**`FolderWatcherService`** (Core, over BCL `FileSystemWatcher`): constructed with path, filter, include-subdirectories and a debounce interval. `FileSystemWatcher` fires several events per save; coalesce per file path and raise once after the file has been quiet for the interval. Take a `TimeProvider` so tests can use fake time. Handle the watcher's `Error` event (buffer overflow) by raising nothing and restarting.

**`ClipboardMonitorService`** (app-side class implementing `ITriggerSource`; one instance app-wide, registered in `AppServices`): subscribes to WinRT `Clipboard.ContentChanged` once. Exposes `IDisposable SuppressSelf()` so any code about to *write* the clipboard can mark its own change to be ignored. `IClipboardService.SetTextAsync` (F4, if landed) should use it. The Edit Text Window's existing handler then subscribes to this service instead of the WinRT event; its `IsAccessingClipboard` flag stays as a second guard.

**`HotkeyActionRouter`** (app-side, DI singleton): `Register(ShortcutKeyActions action, Action handler)` and `bool TryInvoke(ShortcutKeyActions action)`. The existing 11 cases are registered at startup pointing at the same `TrayActions` methods. `HotKeyManager_HotKeyPressed` becomes: resolve action → `router.TryInvoke`. The UI-thread marshalling that `RunOnUiThread` does today stays inside the router.

## Steps

1. `ITriggerSource`, `TriggerPayload`, `TriggerKind` in Core.
2. `FolderWatcherService` + tests in `Tests.Core` using a temp directory and fake time: single write → one event; burst of writes to one file → one event; two files → two events; filter excludes non-matching files; `Stop` silences it.
3. `HotkeyActionRouter`: characterize first — write a test listing the 11 actions and the `TrayActions` method each maps to, from the current `switch`. Then introduce the router and replace the `switch`. Same test passes against the registrations.
4. `ClipboardMonitorService` with `SuppressSelf`. Move the ETW's subscription onto it. Test the suppression logic with a fake event source; do not unit-test the real WinRT event.

## Out of scope

- Any workflow code. Nothing here references `Text_Grab.Core.Workflows`.
- New trigger kinds (after-grab, screen-region-changed, live-transcription phrase) — record them as follow-ups only.
- Changing `HotKeyManager` or how shortcuts are stored.
- Switching to `AddClipboardFormatListener`. Only raise it in your report if `ContentChanged` demonstrably does not fire in a `--windowless` process.

## Verification

- All three test executables pass.
- Manual: every global hotkey still opens its window; the Edit Text Window's "clipboard watcher" toggle still appends copied text, and does not append text the window itself just copied.
- Manual: launch `--windowless`, copy text in another app, and confirm (debug log or breakpoint) that `ClipboardMonitorService` raised an event. Report the result either way.

## Done when

Three sources exist with tests, the hotkey `switch` is gone, and the ETW uses the shared clipboard monitor.

## Traps

- **Feedback loop.** A consumer that writes the clipboard in response to a clipboard event will retrigger itself forever. `SuppressSelf` must cover the asynchronous gap between the write and the event arriving; compare content or use a short suppression window, and test the case where the user copies something *during* that window.
- `FileSystemWatcher` raises events on thread-pool threads and can raise `Changed` while the file is still locked by the writer. The service raises the event; consumers open the file with retry. Document that on the interface.
- `ContentChanged` fires on an arbitrary thread; the ETW handler touches UI. Keep the marshalling the handler has today.
