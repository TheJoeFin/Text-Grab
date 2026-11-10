# F4 — Output services

| | |
|---|---|
| Track | Foundation |
| Size / risk | Medium / Low–medium (clipboard threading) |
| Recommended model | Sonnet |
| Depends on | F1 |
| Unblocks | W1 (output nodes) |
| Branch | `v5/f4-output-services` |

Read [README.md](README.md) first.

## Goal

Put the four outputs a workflow needs behind interfaces declared in Core, so headless code can write the clipboard, save a file, show a toast and POST over HTTP without touching WPF.

## Current state (verify)

- **TTS is already done**: `ITtsEngine` and `TtsService` in Core, `WindowsSpeechEngine` in Core.Windows. Nothing to do.
- **Clipboard**: `Text-Grab/Utilities/ClipboardUtilities.cs` is app-side and uses the WPF `Clipboard`. `OutputUtilities.HandleTextFromOcr` writes the clipboard for grabs.
- **Files**: `FileUtilities.SaveTextFile(text, filename, FileStorageKind)` is in Core.Windows and handles packaged vs unpackaged storage.
- **Toast**: `DesktopNotificationManagerCompat` is in Core.Windows. `Text-Grab/Utilities/NotificationUtilities.cs` is app-side because it reopens windows on activation.
- **HTTP**: no shared client or POST helper. F1 registered one `HttpClient` in the container.

**Corrections found while building F4 (2026-11-09, `v5/f3-6`):**

- **Clipboard.** `ClipboardUtilities` has no write method and no retry pattern. The grab write is WPF `Clipboard.SetDataObject(text, copy: true)`, inline in `OutputUtilities.HandleTextFromOcr` and wrapped in an empty `catch`. WPF's `Clipboard` retries the OLE calls internally, synchronously: 10 attempts, 100 ms apart. `WinRtClipboardService` copies that budget and runs inline on STA callers. `HandleTextFromOcr` calls `WindowUtilities.ShouldShutDown()` right after the write, so a write left queued on the dispatcher could be lost when the app exits.
- **Toast.** Nothing calls `DesktopNotificationManagerCompat`'s registration or notifier methods. The live plumbing is `Microsoft.Toolkit.Uwp.Notifications` (`ToastContentBuilder.Show()` and `ToastNotificationManagerCompat.OnActivated`), which only the app referenced. `ToastService` uses it, so Core.Windows now references the same 7.1.3 (its net5.0-windows asset references no WPF assembly). Both toast-click entry points ignore a toast with no argument, which is what `ToastService` sends.
- **HTTP.** F1's shared `HttpClient` follows redirects, and `TesseractGitHubFileDownloader` relies on it doing so. "No automatic redirect to a different host" cannot be enforced on that client, so `IHttpPostService` is registered over its own `HttpPostService.CreateClient()` client (`AllowAutoRedirect = false`). The service follows same-host 307/308 redirects itself.

## Design

Interfaces in `Text-Grab.Core`, namespace `Text_Grab.Core.Abstractions`:

```csharp
public interface IClipboardService
{
    Task<string?> GetTextAsync(CancellationToken ct = default);
    Task SetTextAsync(string text, CancellationToken ct = default);
}
public interface ITextFileWriter
{
    Task<bool> WriteAsync(string absolutePath, string text, bool append = false, CancellationToken ct = default);
}
public interface IToastService
{
    void Show(string title, string body);
}
public interface IHttpPostService
{
    Task<HttpPostResult> PostAsync(Uri destination, string body, string contentType = "text/plain",
        IReadOnlyDictionary<string, string>? headers = null, CancellationToken ct = default);
}
public sealed record HttpPostResult(int StatusCode, bool IsSuccess, string? ResponseBody);
```

| Interface | Implementation | Project |
|---|---|---|
| `IClipboardService` | `WinRtClipboardService` over `Windows.ApplicationModel.DataTransfer.Clipboard` | Core.Windows |
| `ITextFileWriter` | `TextFileWriter` | Core.Windows |
| `IToastService` | `ToastService` over the existing toast plumbing; shows only, activation stays in the app | Core.Windows |
| `IHttpPostService` | `HttpPostService` over the injected `HttpClient` | Core |

Clipboard threading: WinRT clipboard calls must run on a thread with a message pump. When `UiThreadAccess.IsConfigured` and a UI thread exists, marshal there. When there is none (windowless run), use a dedicated STA thread owned by the service. Both paths need a test.

`HttpPostService`: `http` and `https` only; a timeout (30 s default); response body capped (64 KB); no automatic redirect to a different host. It never logs the request body.

## Steps

1. Add the interfaces and `HttpPostResult` to Core.
2. `HttpPostService` + tests in `Tests.Core` with a fake `HttpMessageHandler` (success, non-2xx, timeout, cancellation, rejected scheme).
3. `TextFileWriter` + tests in `Tests.Core.Windows` writing to a temp folder (create, overwrite, append, missing directory).
4. `WinRtClipboardService` + tests in `Tests.Core.Windows` for the no-UI-thread path. Add a `Tests` (WPF) test for the UI-thread path.
5. `ToastService`. Read `NotificationUtilities` and `DesktopNotificationManagerCompat` first; reuse the existing registration and app identity handling, and do not change what happens when a toast is clicked.
6. Register all four in `AppServices`.
7. Retarget **one** existing caller as proof: the clipboard write in `OutputUtilities.HandleTextFromOcr` goes through `IClipboardService`. Leave every other clipboard call site alone.

## Out of scope

- Mass-migrating UI clipboard or toast call sites.
- Clipboard formats other than plain text (HTML tables, images).
- Retiring `SvgImageLoader`'s private `HttpClient` — it is deliberately restricted.

## Verification

- All three test executables pass.
- Manual: a fullscreen grab still lands on the clipboard; a toast still appears after a grab if that setting is on, and clicking it still opens the Edit Text Window with the text.

## Done when

Four interfaces, four registered implementations, tests for each, and one production caller using `IClipboardService`.

## Traps

- Clipboard tests are flaky if another process holds the clipboard. Retry with a short backoff inside the service (the existing `ClipboardUtilities` shows the pattern), and restore the previous clipboard text at the end of each test.
- Toasts need package identity or a registered AUMID. Unpackaged test hosts may not be able to show one; make `Show` fail quietly and return, and test the composition of the toast content rather than its display.
- `Text-Grab.Core` must not take a Windows reference for `IHttpPostService`. `HttpClient` is in the BCL.
