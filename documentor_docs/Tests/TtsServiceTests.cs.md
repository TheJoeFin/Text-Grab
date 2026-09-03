# Technical Documentation: `Tests/TtsServiceTests.cs`

## Overview

The `TtsServiceTests.cs` file contains unit tests for the `TtsService` class (located in `Text_Grab.Services`). Specifically, it tests the state transition behavior of `TtsService` when handling consecutive Text-to-Speech (TTS) requests and state callbacks using a mock TTS engine.

---

## File Details

* **File Path:** `Tests/TtsServiceTests.cs`
* **Namespace:** `Tests`
* **Dependencies:**
  * `System.Collections.Concurrent`
  * `Text_Grab.Interfaces`
  * `Text_Grab.Services`
  * xUnit framework (`[Fact]`)

---

## Components

### 1. `TtsServiceTests` (Class)

The primary unit test class containing test cases for `TtsService`.

#### Test Methods

##### `DrainCallbackQueuingSpeech_DoesNotPublishIdleBetweenRequests()`

* **Attribute:** `[Fact]`
* **Purpose:** Ensures that when a callback scheduled via `service.RunWhenIdle(...)` immediately queues a new speech request while the service is completing a prior request, the `TtsService.BusyChanged` event does not publish an intermediate idle state (`false`) between requests.
* **Tested Members of `TtsService`:**
  * `service.Engine` (Property setter)
  * `service.BusyChanged` (Event subscription)
  * `service.Speak(string)`
  * `service.RunWhenIdle(Action)`

---

### 2. `ControlledTtsEngine` (Private Nested Class)

A thread-safe test helper class implementing `ITtsEngine` that allows explicit control over the start and completion timing of `SpeakAsync` calls.

#### Fields and Properties

* `private int callCount`: An integer tracking how many times `SpeakAsync` has been called.
* `public TaskCompletionSource FirstStarted`: Signals when the first call to `SpeakAsync` begins.
* `public TaskCompletionSource SecondStarted`: Signals when the second call to `SpeakAsync` begins.
* `public TaskCompletionSource ReleaseFirst`: Controlled by the test to allow the first `SpeakAsync` call to finish.
* `public TaskCompletionSource ReleaseSecond`: Controlled by the test to allow the second `SpeakAsync` call to finish.

#### Methods

##### `public async Task SpeakAsync(string text, CancellationToken ct)`

* **Purpose:** Implements the `ITtsEngine.SpeakAsync` contract for testing.
* **Logic:**
  1. Atomically increments `callCount` using `Interlocked.Increment`.
  2. Selects the appropriate `started` and `release` `TaskCompletionSource` instances based on whether `callCount` is `1` or subsequent calls (`callCount != 1`).
  3. Triggers `started.TrySetResult()` to notify callers that execution has reached this point.
  4. Awaits `release.Task.WaitAsync(ct)` to pause execution until the test explicitly releases the task.

---

## Test Logic and Execution Flow

The `DrainCallbackQueuingSpeech_DoesNotPublishIdleBetweenRequests` test executes through the following steps:

1. **Initialization:**
   * Instantiates `ControlledTtsEngine engine` and `TtsService service`.
   * Sets `service.Engine = engine`.
   * Prepares a thread-safe `ConcurrentQueue<bool> busyEvents` to record all boolean states emitted by `service.BusyChanged`.
   * Sets up a `TaskCompletionSource idle` that completes when `service.BusyChanged` emits `false`.

2. **First Speech Execution:**
   * Calls `service.Speak("first")`.
   * Awaits `engine.FirstStarted.Task` (with a 5-second timeout) to ensure the first speech request has started.

3. **Queue Second Speech via `RunWhenIdle`:**
   * Invokes `service.RunWhenIdle(() => service.Speak("second"))`.
   * Releases the first speech request by calling `engine.ReleaseFirst.TrySetResult()`.
   * Awaits `engine.SecondStarted.Task` (with a 5-second timeout) to confirm the second request started.

4. **First Assertion:**
   * Asserts `Assert.Equal([true], busyEvents);`.
   * **Verification:** Confirms `BusyChanged` was only triggered once with `true` when "first" started, and was **not** triggered with `false` between "first" and "second".

5. **Second Speech Completion:**
   * Releases the second speech request by calling `engine.ReleaseSecond.TrySetResult()`.
   * Awaits `idle.Task` (with a 5-second timeout) until `BusyChanged` raises `false`.

6. **Second Assertion:**
   * Asserts `Assert.Equal([true, false], busyEvents);`.
   * **Verification:** Confirms the state transitions strictly followed `[true, false]` across the entire lifecycle of both speech requests.