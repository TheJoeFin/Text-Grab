# Technical Documentation: `Text-Grab/Utilities/OutputUtilities.cs`

## Overview

The `OutputUtilities` class in `Text-Grab.Utilities` provides utility functionality for handling text extracted from Optical Character Recognition (OCR) processes. It formats the recognized text and routes it to either a designated WPF `TextBox` UI control or system-level outputs (such as the Windows Clipboard and Toast Notifications).

---

## Class Information

* **Namespace:** `Text-Grab.Utilities`
* **Class Name:** `OutputUtilities`
* **Access Modifier:** `public`

---

## Dependencies & Imports

* `System.Windows` — Provides core WPF framework types (e.g., `Clipboard`).
* `System.Windows.Controls` — Provides UI control types (e.g., `TextBox`).
* `Text_Grab.Services` — Namespace containing application services used within this class.

---

## Methods

### `HandleTextFromOcr`

Processes text resulting from an OCR operation, applies line-formatting rules based on flags, and outputs the result to a specified UI `TextBox` or to system notifications and the clipboard.

#### Method Signature

```csharp
public static void HandleTextFromOcr(
    string grabbedText, 
    bool isSingleLine, 
    bool isTable, 
    TextBox? destinationTextBox = null
)
```

#### Parameters

| Parameter | Type | Optional | Description |
| :--- | :--- | :--- | :--- |
| `grabbedText` | `string` | No | The raw or preliminary text extracted via OCR. |
| `isSingleLine` | `bool` | No | Flag indicating whether the output text should be formatted into a single line. |
| `isTable` | `bool` | No | Flag indicating if the extracted text represents table data. |
| `destinationTextBox` | `TextBox?` | Yes (Default: `null`) | An optional WPF `TextBox` control where the processed text should be inserted. |

---

## Logic and Execution Flow

1. **Text Formatting Check**:
   * Evaluates if `isSingleLine` is `true` AND `isTable` is `false`.
   * If both conditions are met, it transforms `grabbedText` by calling the string extension method `MakeStringSingleLine()`.

2. **TextBox Destination Processing** (If `destinationTextBox` is provided / not `null`):
   * Inserts `grabbedText` at the current cursor position or replaces selected text using `destinationTextBox.SelectedText = grabbedText`.
   * Moves the selection cursor immediately to the end of the newly inserted text via `destinationTextBox.Select(destinationTextBox.SelectionStart + grabbedText.Length, 0)`.
   * Focuses the `TextBox` by calling `destinationTextBox.Focus()`.
   * Exits the method immediately (`return`).

3. **Fallback Destination Processing** (If `destinationTextBox` is `null`):
   * **Clipboard Handling**: Checks setting `AppUtilities.TextGrabSettings.NeverAutoUseClipboard`. If `false`, attempts to copy `grabbedText` to the Windows Clipboard using `Clipboard.SetDataObject(grabbedText, true)`. Exceptions thrown during clipboard operations are caught and suppressed (`try { ... } catch { }`).
   * **Notification Handling**: Checks setting `AppUtilities.TextGrabSettings.ShowToast`. If `true`, calls `NotificationUtilities.ShowToast(grabbedText)`.
   * **Application Shutdown Check**: Calls `WindowUtilities.ShouldShutDown()` to check if the application should close after output completion.