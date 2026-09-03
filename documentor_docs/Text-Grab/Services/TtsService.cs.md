# Technical Documentation: `TtsService.cs`

**File Path:** `Text-Grab/Services/TtsService.cs`  
**Namespace:** `Text-Grab.Services`  
**Class:** `TtsService`

---

## 1. Overview

The `TtsService` class manages asynchronous Text-to-Speech (TTS) operations within the application. It acts as an orchestrator that accepts incoming text strings, enforces word count limits based on user settings, enqueues the text into an unbounded producer-consumer channel, and processes speech sequentially using an underlying TTS engine (`ITtsEngine`).

Key capabilities include:
- **Sequential Queuing:** Queues multiple speech requests and processes them sequentially in a background loop.
- **Word Truncation:** Enforces word limits using application settings prior to enqueuing.
- **Playback Control:** Supports cancelling active speech and clearing pending queued items.
- **State & Idle Tracking:** Tracks busy/idle state transitions and provides event-driven hooks to execute callbacks when all speech operations complete.

---

## 2. Dependencies & Dependencies Injection

| Type | Field / Property | Description |
| :--- | :--- | :--- |
| `ITtsEngine` | `_engine` / `Engine` | Implementation of the speech synthesis engine. Defaults to `WindowsSpeechEngine()`. Settable via the `Engine` property. |
| `Settings.Default` | Static dependency | Used by `ApplyWordLimit` to retrieve `TtsSpeakWordLimit`. |

---

## 3. Class Fields & Private State

```csharp
private ITtsEngine _engine = new WindowsSpeechEngine();
private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();
private readonly CancellationTokenSource _cts = new();
private CancellationTokenSource _speechCts = new();
private readonly object _lock = new();
private int _pendingCount = 0;
private bool _isBusy;

private event Action? Drained;
```

- **`_engine`**: The underlying engine used to synthesize speech. Defaults to `WindowsSpeechEngine`.
- **`_queue`**: An unbounded channel (`Channel<string>`) used to safely queue text items across threads.
- **`_cts`**: A `CancellationTokenSource` associated with the overall lifecycle of the `DrainLoopAsync` task.
- **`_speechCts`**: A `CancellationTokenSource` specifically passed to `ITtsEngine.SpeakAsync` to cancel current active speech synthesis. Recreated upon cancellation.
- **`_lock`**: Sync root used for thread safety when modifying `_pendingCount`, `_isBusy`, subscriber events, and state checking.
- **`_pendingCount`**: Tracks the total number of items that are currently in the queue plus any item currently being spoken by the engine.
- **`_isBusy`**: Indicates whether the service is actively processing or waiting to process queued speech requests.
- **`Drained`**: Internal private event raised when `_pendingCount` reaches `0`.

---

## 4. Public API Reference

### 4.1 Events

#### `BusyChanged`
```csharp
public event Action<bool>? BusyChanged;
```
- **Description:** Raised when the service transitions between idle (`false`) and busy (`true`).
- **Threading Note:** May fire on a background thread. UI components subscribing to this event must marshal execution back to their UI/Dispatcher thread.

---

### 4.2 Properties

#### `IsBusy`
```csharp
public bool IsBusy { get; }
```
- **Type:** `bool`
- **Access:** Read-only (`get`)
- **Description:** Returns the current busy state (`_isBusy`) in a thread-safe manner using `_lock`.

#### `Engine`
```csharp
public ITtsEngine Engine { set; }
```
- **Type:** `ITtsEngine`
- **Access:** Write-only (`set`)
- **Description:** Allows swapping the underlying speech engine implementation dynamically.

---

### 4.3 Constructor

```csharp
public TtsService()
```
- **Behavior:** Initializes the instance and immediately spawns the background processing loop `DrainLoopAsync` using `Task.Run()`.

---

### 4.4 Methods

#### `Speak(string text)`
```csharp
public void Speak(string text)
```
- **Parameters:**
  - `text` (`string`): The text string to be spoken.
- **Behavior:**
  1. Validates that `text` is not null, empty, or whitespace. Returns immediately if invalid.
  2. Passes `text` through `ApplyWordLimit(text)` to enforce word limits.
  3. Enters `_lock`:
     - Increments `_pendingCount`.
     - Calls `_queue.Writer.TryWrite(text)`.
     - If writing to the channel fails, decrements `_pendingCount` and calls `PublishIdleIfDrained()`.
     - If writing succeeds and `_isBusy` is `false`, sets `_isBusy = true` and invokes `BusyChanged?.Invoke(true)`.

#### `Stop()`
```csharp
public void Stop()
```
- **Behavior:**
  1. Cancels `_speechCts` to interrupt active speech in `_engine.SpeakAsync`.
  2. Reinstantiates `_speechCts = new CancellationTokenSource()`.
  3. Enters `_lock`:
     - Drains all pending (unread) items from `_queue.Reader` using `TryRead`, decrementing `_pendingCount` for each removed item.
     - Calls `PublishIdleIfDrained()`.
  - *Note:* If an item is currently being spoken when `Stop()` is called, its complete cancellation cleanup and state update occur when `SpeakAsync` returns in `DrainLoopAsync`.

#### `RunWhenIdle(Action action)`
```csharp
public void RunWhenIdle(Action action)
```
- **Parameters:**
  - `action` (`Action`): The delegate to execute when the TTS queue is completely empty and inactive.
- **Behavior:**
  - Enters `_lock`:
    - If `_pendingCount == 0`, executes `action()` synchronously immediately.
    - If `_pendingCount > 0`, registers a one-time event handler on `Drained` that unsubscribes itself upon invocation and executes `action()`.
  - Atomicity within `_lock` ensures no race conditions between checking `_pendingCount` and subscribing to `Drained`.

---

## 5. Private Helper Methods

### `ApplyWordLimit(string text)`
```csharp
private static string ApplyWordLimit(string text)
```
- **Parameters:** `text` (`string`)
- **Returns:** `string` (original or truncated text)
- **Logic:**
  1. Retrieves `Settings.Default.TtsSpeakWordLimit`.
  2. If `wordLimit <= 0`, returns `text` unmodified.
  3. Splits `text` on whitespace using `text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)`.
  4. If the resulting word array length exceeds `wordLimit`, truncates the array to `words[..wordLimit]` and joins them with spaces (`' '`). Otherwise, returns `text`.

### `DrainLoopAsync()`
```csharp
private async Task DrainLoopAsync()
```
- **Execution:** Runs asynchronously on a background thread started in the constructor.
- **Logic:**
  1. Interates through `_queue.Reader.ReadAllAsync(lifecycleCt)`.
  2. Calls `await _engine.SpeakAsync(text, _speechCts.Token)`.
  3. Catches and suppresses `OperationCanceledException` (allows loop execution to continue processing or draining remaining queue items).
  4. Catches and suppresses generic `Exception` instances (prevents errors from crashing the background processing loop).
  5. In a `finally` block locked under `_lock`:
     - Decrements `_pendingCount`.
     - Calls `PublishIdleIfDrained()`.

### `PublishIdleIfDrained()`
```csharp
private void PublishIdleIfDrained()
```
- **Logic:**
  1. Verifies if `_pendingCount == 0`. If not, returns immediately.
  2. Invokes the private `Drained` event (notifying callbacks registered via `RunWhenIdle`).
  3. Evaluates if `_pendingCount == 0` and `_isBusy` is `true`.
     - If true, updates `_isBusy = false` and raises `BusyChanged?.Invoke(false)`.
  - *Note:* Re-verifying `_pendingCount == 0` after invoking `Drained` prevents premature state transitions if a `Drained` subscriber synchronously enqueues new text via `Speak()`.

---

## 6. Execution & Synchronization Flow

```
[ Call Speak("Hello World") ]
           │
           ▼
 Check string empty/null ──► Exits if empty
           │
           ▼
   ApplyWordLimit()
           │
           ▼
     lock (_lock) ──► Increment _pendingCount
           │      ──► Write to Channel (_queue)
           │      ──► If !_isBusy: _isBusy = true, BusyChanged(true)
           ▼
┌──────────────────────────────────────────────┐
│ Background Loop: DrainLoopAsync()            │
└──────────────────────────────────────────────┘
           │
           ▼
 Channel.Reader.ReadAllAsync() yields item
           │
           ▼
 await _engine.SpeakAsync(text, _speechCts.Token)
           │
           ▼
        finally
           │
           ▼
     lock (_lock) ──► Decrement _pendingCount
                  ──► PublishIdleIfDrained()
                          │
                          ▼
                  If _pendingCount == 0:
                  1. Fire Drained subscribers (RunWhenIdle)
                  2. If still empty & _isBusy:
                     - Set _isBusy = false
                     - Fire BusyChanged(false)
```