# Detailed Technical Documentation: `HistoryService.cs`

**File Location:** `Text-Grab/Services/HistoryService.cs`  
**Namespace:** `Text-Grab.Services`  
**Class:** `HistoryService` (partial class, implements `IDisposable`)

---

## 1. Overview

`HistoryService` is a core service in Text-Grab responsible for managing the persistence, in-memory caching, retrieving, and purging of capture history items. It maintains two distinct categories of history:
1. **Text-Only History** (`HistoryTextOnly`): Captured text originating from edit windows (`EditTextWindow`).
2. **Visual / Image History** (`HistoryWithImage`): Screen captures, full-screen captures, Grab Frames, and PDF document grabs.

The service handles serialization/deserialization to JSON files, managed sidecar files for word border data (`.wordborders.json`), unmanaged GDI bitmap cache management, idle cache release, and debounced asynchronous file saving.

---

## 2. Architecture & Class Setup

* **Type:** `public partial class HistoryService : IDisposable`
* **Interfaces:** `IDisposable`
* **Dependencies:** Uses `FileUtilities`, `LanguageUtilities`, `NativeMethods`, and custom WPF/UI components (`MenuItem`, `SymbolIcon`).

### Key Constants & Configuration

| Constant / Static Field | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `maxHistoryTextOnly` | `int` | `100` | Maximum number of text-only history items retained. |
| `maxHistoryWithImages` | `int` | `10` | Maximum number of non-PDF image captures retained. |
| `maxHistoryPdfDocuments` | `int` | `10` | Maximum number of PDF history items retained. |
| `WordBorderInfoFileSuffix` | `string` | `".wordborders.json"` | File suffix used for word border sidecar files. |
| `historyCacheCheckInterval`| `TimeSpan` | `1 minute` | Interval at which the cache release timer checks for idle history data. |
| `historyCacheIdleLifetime` | `TimeSpan` | `2 minutes` | Idle time threshold before unloading history from memory. |

---

## 3. Serialization Configuration & Recovery

### JSON Serialization Settings
`HistoryJsonOptions` is configured with:
* `AllowTrailingCommas = true`
* `WriteIndented = true`
* Custom converters:
  * `HistoryLanguageKindJsonConverter` (handles enum string/numeric conversion with fallback logic)
  * `JsonStringEnumConverter`

### Custom Converter: `HistoryLanguageKindJsonConverter`
A nested `JsonConverter<LanguageKind>` handles backwards compatibility and invalid values during deserialization:
* Converts string or numeric tokens into `LanguageKind`.
* If parsing fails or the token is null/invalid, sets `HistoryLanguageKindFallbackUsed.Value = true` and falls back to `LanguageKind.Global`.
* Serializes values out as strings.

### Asynchronous Error Recovery (`LoadHistoryWithRecovery`)
When `JsonSerializer.Deserialize` fails to parse a history JSON file (due to partial corruption or malformed items), `LoadHistoryAsync` falls back to `LoadHistoryWithRecovery`:
1. Parses the raw JSON into a `JsonDocument`.
2. Iterates item-by-item over the array elements.
3. Deserializes valid `HistoryInfo` items individually while skipping corrupt elements.
4. Marks `needsRewrite = true` to re-save the cleaned collection.

---

## 4. In-Memory Caching & Idle Release Workflow

To optimize memory utilization, `HistoryService` lazily loads history items when requested and automatically unloads them when idle.

```
[ Call Query/Save Method ] 
          │
          ▼
   Ensure Loaded? ─── No ───► Load from File Storage
          │
          ▼
  TouchHistoryCache() ──► Updates _lastHistoryAccessUtc & starts historyCacheReleaseTimer
          │
          ▼
[ Idle Timer Ticks Every 1 Min ]
          │
          ├─► Has pending write? ─── Yes ───► Do nothing
          └─► Idle > 2 minutes? ─── Yes ───► Call ReleaseLoadedHistoriesCore() (Frees Memory)
```

* **`TouchHistoryCache()`**: Updates `_lastHistoryAccessUtc` to `DateTimeOffset.UtcNow` and starts `historyCacheReleaseTimer`.
* **`HistoryCacheReleaseTimer_Tick`**: Runs every minute. If `_hasPendingWrite` is `false` and memory has been idle for more than `historyCacheIdleLifetime` (2 minutes), it executes `ReleaseLoadedHistoriesCore()`.
* **`ReleaseLoadedHistoriesCore()`**: Flushes transient payloads (`ClearTransientImage`, `ClearTransientWordBorderData`), clears `HistoryWithImage` and `HistoryTextOnly` lists, and resets load flags (`_textHistoryLoaded = false`, `_imageHistoryLoaded = false`).

---

## 5. Persistence & Debounced Saving

### Save Timer (Debouncing)
When history is modified, `MarkHistoryDirty()` is invoked:
1. Flags `_hasPendingWrite = true`.
2. Calls `TouchHistoryCache()`.
3. Resets and starts `saveTimer` (interval: 500ms).

When `saveTimer` ticks (`SaveTimer_Tick`):
1. Stops the timer.
2. Calls `WriteHistory()`.
3. Clears native cached bitmap resources via `DisposeCachedBitmap()`.

### Synchronous Persistence (`WriteHistory`)
If `_hasPendingWrite` is true:
* **Text History:** Normalizes compatibility data, orders items by capture date, takes the last `maxHistoryTextOnly` (100) items, serializes them, and writes `HistoryTextOnly.json`.
* **Image History:**
  1. Identifies and removes excess image history beyond limits (`maxHistoryWithImages` and `maxHistoryPdfDocuments`).
  2. Clears artifacts (images and word border sidecars) for removed items.
  3. Writes sidecar files for word border data (`{historyId}.wordborders.json`).
  4. Writes `HistoryWithImage.json`.
  5. Scans for and deletes orphaned `.wordborders.json` files using `DeleteUnusedWordBorderFiles()`.

---

## 6. Detailed Method Specifications

### Bitmap Management
* **`CacheLastBitmap(Bitmap bmp)`**: Takes an in-memory `Bitmap`, obtains its native GDI handle (`bmp.GetHbitmap()`), disposes any existing cached bitmap and handle, and stores the new instance and handle.
* **`DisposeCachedBitmap()`**: Releases the native GDI object handle via `NativeMethods.DeleteObject(_cachedBitmapHandle)` and disposes the `CachedBitmap` instance.

### History Retrieval Methods
* **`GetEditWindows()`**: Returns a cloned list of `HistoryTextOnly` items.
* **`GetLastFullScreenGrabInfo()`**: Returns the last `HistoryInfo` from `HistoryWithImage` where `SourceMode == TextGrabMode.Fullscreen`.
* **`HasAnyFullscreenHistory()`**: Checks if any item in `HistoryWithImage` has `SourceMode == TextGrabMode.Fullscreen`.
* **`GetLastHistoryAsGrabFrame()`**: Finds the most recent non-PDF grab and opens it in a new `GrabFrame` window. Returns `true` if displayed successfully.
* **`GetLastTextHistory()`**: Returns the text content string of the last item in `HistoryTextOnly`.
* **`GetRecentGrabs()`**: Returns a list of non-PDF visual history items.
* **`GetRecentPdfDocuments()`**: Returns a list of PDF-based visual history items.
* **`GetImageHistoryById(string historyId)` / `GetTextHistoryById(string historyId)`**: Searches active collections for matching `ID`.
* **`GetWordBorderInfosAsync(HistoryInfo history)`**: Reads word border boundary metadata:
  1. Tries reading from the sidecar file referenced in `history.WordBorderInfoFileName`.
  2. If sidecar file reading fails or is missing, falls back to deserializing inline JSON from `history.WordBorderInfoJson`.

### History Saving Methods
* **`SaveToHistory(GrabFrame grabFrameToSave)`**: Converts a `GrabFrame` into a `HistoryInfo` record. Generates or reuses image paths, persists word border sidecar files, saves the bitmap to file storage, and inserts the record into `HistoryWithImage`.
* **`SaveToHistory(HistoryInfo infoFromFullscreenGrab)`**: Persists full-screen captures. Writes the image content to history file storage, links word border data, and appends to `HistoryWithImage`.
* **`SaveToHistory(EditTextWindow etwToSave)`**: Converts an `EditTextWindow` into a `HistoryInfo` record. Deduplicates identical text content (updates timestamp instead of creating duplicates) and updates `HistoryTextOnly`.

### History Deletion & Artifact Cleanup
* **`DeleteHistory()`**: Stops timers, clears pending write flags, unloads in-memory history, disposes bitmaps, and deletes the entire history storage directory via `FileUtilities.TryDeleteHistoryDirectory()`.
* **`RemoveTextHistoryItem(HistoryInfo historyItem)`**: Removes a specific text item and marks history dirty.
* **`RemoveImageHistoryItem(HistoryInfo historyItem)`**: Removes a visual history item, clears transient payloads, deletes image and sidecar files from disk, and marks history dirty.
* **`DeleteHistoryArtifacts(HistoryInfo historyItem)`**: Removes associated `.bmp` and `.wordborders.json` files for a specified `HistoryInfo` item.
* **`DeleteUnusedWordBorderFiles(IEnumerable<HistoryInfo> historyItems)`**: Scans the history directory for `*.wordborders.json` files that are no longer associated with active history records and deletes them.

### UI Integration Methods
* **`PopulateMenuItemWithRecentGrabs(MenuItem recentGrabsMenuItem)`**: Populates a WPF `MenuItem` drop-down with recent visual grabs.
* **`PopulateMenuItemWithRecentPdfs(MenuItem recentPdfsMenuItem)`**: Populates a WPF `MenuItem` drop-down with recent PDF documents.
* **`ClearRecentGrabsMenuItems(MenuItem recentGrabsMenuItem)`**: Unhooks click event handlers and clears items from a history `MenuItem`.
* **`RecentGrabMenuItem_Click(object sender, RoutedEventArgs args)`**: Click handler for dynamically generated UI menu items. Opens the associated history item inside a new `GrabFrame`.

---

## 7. Resource Cleanup (`IDisposable`)

The class implements `IDisposable` to clean up managed timers, write unpersisted history data, and release unmanaged GDI graphics resources.

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    saveTimer.Stop();
    saveTimer.Tick -= SaveTimer_Tick;

    historyCacheReleaseTimer.Stop();
    historyCacheReleaseTimer.Tick -= HistoryCacheReleaseTimer_Tick;

    if (_hasPendingWrite)
        WriteHistory();

    DisposeCachedBitmap();
    ReleaseLoadedHistoriesCore();

    GC.SuppressFinalize(this);
}
```