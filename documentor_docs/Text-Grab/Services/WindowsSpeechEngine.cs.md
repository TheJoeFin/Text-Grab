# Technical Documentation: `WindowsSpeechEngine.cs`

## Overview

The `WindowsSpeechEngine` class provides text-to-speech (TTS) playback functionality within the `Text-Grab` application. It implements the `ITtsEngine` interface using native Windows Universal Windows Platform (UWP / WinRT) speech synthesis and media playback APIs (`Windows.Media.SpeechSynthesis` and `Windows.Media.Playback`).

---

## File Details

* **File Path:** `Text-Grab/Services/WindowsSpeechEngine.cs`
* **Namespace:** `Text_Grab.Services`
* **Implemented Interface:** `ITtsEngine`

---

## Class Architecture & Dependencies

### Direct Dependencies
* **`Text_Grab.Interfaces.ITtsEngine`**: Interface that defines the contract for text-to-speech engines.
* **`Text_Grab.Properties.Settings`**: Application configuration settings used to retrieve voice options and speech rate.
* **`Windows.Media.SpeechSynthesis`**: WinRT APIs used to generate an audio stream from text input (`SpeechSynthesizer`, `VoiceInformation`, `SpeechSynthesisStream`).
* **`Windows.Media.Playback`**: WinRT API used to play the generated speech audio stream (`MediaPlayer`).
* **`Windows.Media.Core`**: API used to create media sources (`MediaSource`).

---

## Key Components & Logic

### Configurable Settings Consumed
The engine relies on user preferences stored in `Settings.Default`:

1. **`Settings.Default.TtsVoiceName`** (`string`): The display name of the preferred voice. If a voice matching this display name is present in `SpeechSynthesizer.AllVoices`, it is assigned to `synthesizer.Voice`.
2. **`Settings.Default.TtsSpeakingRate`** (`double`): Controls the speed of speech generation. The value is applied only if it falls within the valid range of `0.5` to `6.0` inclusive.

---

## Method Documentation

### `SpeakAsync(string text, CancellationToken ct)`

Synthesizes the provided text into speech audio using Windows Speech APIs and plays it asynchronously until completed, failed, or canceled.

#### Parameters
* **`text`** (`string`): The text string to be converted to speech and played back.
* **`ct`** (`CancellationToken`): Token to monitor for cancellation requests.

#### Return Value
* **`Task`**: Represents the asynchronous operation of synthesizing and playing the audio.

---

### Step-by-Step Execution Flow

1. **Synthesizer Initialization & Configuration**:
   * Instantiates a `SpeechSynthesizer` object.
   * Evaluates `Settings.Default.TtsVoiceName`. If non-empty, searches `SpeechSynthesizer.AllVoices` for a matching `DisplayName` and configures `synthesizer.Voice`.
   * Evaluates `Settings.Default.TtsSpeakingRate`. If `0.5 <= speakingRate <= 6.0`, applies it to `synthesizer.Options.SpeakingRate`.

2. **Audio Stream Generation**:
   * Calls `synthesizer.SynthesizeTextToStreamAsync(text)` to convert the input text into a `SpeechSynthesisStream`.

3. **Pre-Playback Cancellation Check**:
   * Evaluates `ct.ThrowIfCancellationRequested()`. If cancellation was requested during or immediately after stream synthesis, an `OperationCanceledException` is thrown before playback starts.

4. **Media Player Setup**:
   * Creates a `TaskCompletionSource<bool>` (`tcs`) to manage task completion state.
   * Instantiates a `MediaPlayer` and a `MediaSource` from the speech stream and its content type.
   * Assigns `mediaSource` to `player.Source`.

5. **Event Subscriptions**:
   * **`MediaEnded`**: Triggers `tcs.TrySetResult(true)` when playback finishes normally.
   * **`MediaFailed`**: Triggers `tcs.TrySetException(new Exception(e.ErrorMessage))` if an error occurs during media playback.

6. **Cancellation Registration**:
   * Registers a callback on the provided `CancellationToken`:
     * Pauses `player`.
     * Calls `tcs.TrySetCanceled()`.

7. **Playback Execution**:
   * Calls `player.Play()`.
   * Awaits `tcs.Task` until playback ends, fails, or is canceled.

---

## Resource & Lifecycle Management

All disposable WinRT resources and helper objects are managed using C# `using` declarations to ensure proper cleanup upon method exit:

* `SpeechSynthesizer`
* `SpeechSynthesisStream`
* `MediaPlayer`
* `MediaSource`
* `CancellationTokenRegistration`