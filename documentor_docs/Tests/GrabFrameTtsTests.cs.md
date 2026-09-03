# Technical Documentation: `Tests/GrabFrameTtsTests.cs`

## Overview

The `GrabFrameTtsTests.cs` file contains unit tests for testing the Text-to-Speech (TTS) evaluation logic in the `GrabFrame` view of the application. Specifically, it tests the `GrabFrame.ShouldSpeakCurrentFrameWhenEnabled` method to ensure that frame text is spoken only under specific state transitions and non-empty text conditions.

---

## File Details

- **File Path:** `Tests/GrabFrameTtsTests.cs`
- **Namespace:** `Tests`
- **Imported Namespaces:** `Text_Grab.Views`
- **Test Framework:** xUnit (`[Theory]`, `[InlineData]`, `Assert.Equal`)

---

## Class Breakdown

### `GrabFrameTtsTests`

`public class GrabFrameTtsTests`

This class serves as the test container for Text-to-Speech logic associated with `GrabFrame`.

---

## Test Methods

### `ShouldSpeakCurrentFrameWhenEnabled_RequiresUncheckedToCheckedTransition`

```csharp
[Theory]
[InlineData(false, true, "Current frame text", true)]
[InlineData(true, true, "Current frame text", false)]
[InlineData(true, false, "Current frame text", false)]
[InlineData(false, true, "", false)]
[InlineData(false, true, "   ", false)]
public void ShouldSpeakCurrentFrameWhenEnabled_RequiresUncheckedToCheckedTransition(
    bool wasSpeakEnabled,
    bool isSpeakEnabled,
    string frameText,
    bool expected)
```

#### Purpose
Validates the behavior of `GrabFrame.ShouldSpeakCurrentFrameWhenEnabled`. It ensures that text-to-speech is triggered (`true`) only when the speech option transitions from disabled (`false`) to enabled (`true`) and valid non-whitespace text is present in the frame.

#### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| `wasSpeakEnabled` | `bool` | The previous state of the speak/TTS toggle. |
| `isSpeakEnabled` | `bool` | The current state of the speak/TTS toggle. |
| `frameText` | `string` | The text content contained within the current frame. |
| `expected` | `bool` | The expected boolean result returned by `GrabFrame.ShouldSpeakCurrentFrameWhenEnabled`. |

#### Tested Scenarios (`[InlineData]`)

1. **`InlineData(false, true, "Current frame text", true)`**
   - **Previous State:** `false` (Disabled)
   - **Current State:** `true` (Enabled)
   - **Text:** `"Current frame text"`
   - **Expected Output:** `true`
   - **Reason:** Correct state transition (`false` $\rightarrow$ `true`) with valid non-empty text.

2. **`InlineData(true, true, "Current frame text", false)`**
   - **Previous State:** `true` (Enabled)
   - **Current State:** `true` (Enabled)
   - **Text:** `"Current frame text"`
   - **Expected Output:** `false`
   - **Reason:** State was already enabled (`true` $\rightarrow$ `true`), so no transition occurred.

3. **`InlineData(true, false, "Current frame text", false)`**
   - **Previous State:** `true` (Enabled)
   - **Current State:** `false` (Disabled)
   - **Text:** `"Current frame text"`
   - **Expected Output:** `false`
   - **Reason:** Feature was disabled (`true` $\rightarrow$ `false`).

4. **`InlineData(false, true, "", false)`**
   - **Previous State:** `false` (Disabled)
   - **Current State:** `true` (Enabled)
   - **Text:** `""` (Empty string)
   - **Expected Output:** `false`
   - **Reason:** Transition occurred, but the frame text is empty.

5. **`InlineData(false, true, "   ", false)`**
   - **Previous State:** `false` (Disabled)
   - **Current State:** `true` (Enabled)
   - **Text:** `"   "` (Whitespace string)
   - **Expected Output:** `false`
   - **Reason:** Transition occurred, but the frame text contains only whitespace.

---

## Execution & Assertions

The test calls the static method `GrabFrame.ShouldSpeakCurrentFrameWhenEnabled` using the provided parameters and compares the return value against `expected` using `Assert.Equal`:

```csharp
Assert.Equal(
    expected,
    GrabFrame.ShouldSpeakCurrentFrameWhenEnabled(
        wasSpeakEnabled,
        isSpeakEnabled,
        frameText));
```