# Technical Documentation: `Text-Grab/Interfaces/ITtsEngine.cs`

## Overview

The `ITtsEngine` interface defines a simple software contract for Text-To-Speech (TTS) engine implementations within the `Text_Grab` project. It establishes a standard asynchronous method for converting a string of text into audible speech with cancellation support.

---

## File Details

* **File Path:** `Text-Grab/Interfaces/ITtsEngine.cs`
* **Namespace:** `Text_Grab.Interfaces`

---

## Dependencies

* `System.Threading`: Provides the `CancellationToken` struct used to handle operation cancellations.
* `System.Threading.Tasks`: Provides the `Task` class used for managing asynchronous operations.

---

## Interface Definition

```csharp
namespace Text_Grab.Interfaces;

public interface ITtsEngine
{
    Task SpeakAsync(string text, CancellationToken ct);
}
```

### Access Modifier
* `public`: The interface is accessible throughout the application assembly and any referencing projects.

---

## Member Specifications

### `SpeakAsync`

```csharp
Task SpeakAsync(string text, CancellationToken ct);
```

#### Description
An asynchronous method that processes a text string for speech output.

#### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| `text` | `string` | The text string that the TTS engine should vocalize. |
| `ct` | `System.Threading.CancellationToken` | A token that allows the speech operation to be cancelled before or during execution. |

#### Return Value
* **Type:** `System.Threading.Tasks.Task`
* **Description:** Represents the ongoing asynchronous speech operation. Completes when the text vocalization finishes or is cancelled.

---

## How It Works

1. **Abstraction**: `ITtsEngine` serves as an abstraction layer for text-to-speech capabilities. Concrete classes implementing `ITtsEngine` must supply the execution logic for converting text to speech.
2. **Asynchronous Execution**: By returning a `Task`, the implementation ensures non-blocking execution, allowing caller threads (such as UI threads) to remain responsive.
3. **Cancellation Support**: The inclusion of `CancellationToken` allows callers to interrupt or cancel speech synthesis midway through execution if needed.