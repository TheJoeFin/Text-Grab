# Technical Documentation: `Text-Grab/Utilities/NotificationUtilities.cs`

## Overview

The `NotificationUtilities` class provides a static helper method to display Windows Toast notifications in the **Text-Grab** application. Its primary responsibility is to package copied text into a system notification while safely handling string limits and XML payload size constraints enforced by the Windows Toast notification platform.

---

## Class Structure & Metadata

* **File Path:** `Text-Grab/Utilities/NotificationUtilities.cs`
* **Namespace:** `Text_Grab.Utilities`
* **Access Modifier:** `internal`
* **Class Type:** `static`

### Dependencies
* `Microsoft.Toolkit.Uwp.Notifications`: Provides `ToastContentBuilder` for constructing and displaying Windows Toast notifications.
* `System`: Core system utilities (`Convert`, `Span`, etc.).
* `System.Text`: Character encoding classes (`Encoding`, `UTF8`).

---

## Methods

### `ShowToast(string copiedText)`

Constructs and displays a Windows Toast notification containing the specified copied text.

* **Access Modifier:** `internal static`
* **Return Type:** `void`
* **Parameters:**
  * `copiedText` (`string`): The text captured or copied that needs to be attached to and previewed in the toast notification.

---

## Detailed Mechanics and Execution Flow

The `ShowToast` method processes the text through several stages to ensure the notification payload remains valid and within system limits:

```
[copiedText]
     │
     ├── 1. Convert string to Base64 (trim trailing '=') ──> [encodedString]
     │
     ├── 2. Truncate for UI preview (max 150 chars) ───────> [toastBody]
     │
     ├── 3. Build Toast XML Payload ──────────────────────> [ToastContentBuilder]
     │
     └── 4. Measure Toast XML size (in UTF-8 bytes)
              │
              ├── Size <= 5000 bytes ──> [Show Toast]
              │
              └── Size > 5000 bytes
                    │
                    ├── Recalculate max allowed bytes for argument
                    ├── Safely encode string using Encoder.Convert()
                    ├── Re-encode to Base64
                    ├── Rebuild Toast XML Payload
                    └── [Show Toast]
```

### 1. Initial Argument Encoding
The method converts `copiedText` into a UTF-8 byte array and then encodes it to a Base64 string (`encodedString`).
* Trailing Base64 padding characters (`=`) are stripped using `.TrimEnd('=')`. This prevents unnecessary URL/XML encoding overhead (`=` translates to `%3D` in toast XML).

### 2. Preview Text Truncation
To prevent visual clutter in the notification UI, `copiedText` is truncated for display:
* If `copiedText.Length` exceeds **150 characters**, it is trimmed to the first 150 characters and appended with `...`.
* If `copiedText.Length` is **150 characters or fewer**, the string is used as-is.
* The resulting string is assigned to `toastBody`.

### 3. Toast Payload Construction
An initial toast payload is generated using `ToastContentBuilder`:
* **Argument:** `"text"` set to `encodedString`.
* **Title Line:** `"Text Grab"`.
* **Body Line:** `toastBody`.

### 4. Payload Size Limit Validation
Windows toast XML payloads have a strict maximum size limit of **5000 bytes**.

1. The raw XML size of the constructed toast is calculated in UTF-8 bytes:
   ```csharp
   int toastSizeInBytes = Encoding.UTF8.GetByteCount(toast.Content.GetContent());
   ```
2. If `toastSizeInBytes` exceeds **5000 bytes**, payload truncation logic is executed:
   * **Calculate available byte capacity:**
     `bytesFree = 5000 - (toastSizeInBytes - encodedString.Length)`
   * **Calculate maximum raw bytes:**
     Base64 encodes 3 bytes into 4 characters. The base formula calculates maximum plaintext bytes as:
     `maxTextBytes = bytesFree / 4 * 3`
   * **Account for unpadded character capacity:**
     If `bytesFree % 4 >= 2`, 1 or 2 additional text bytes can fit within the remaining space, so `maxTextBytes` is incremented by `(bytesFree % 4 - 1)`.
   * **Safely convert string within byte bounds:**
     A byte array `plainTextBytes` of size `maxTextBytes` is allocated. `Encoding.UTF8.GetEncoder().Convert()` is called to convert as many complete UTF-8 characters as possible from `copiedText.AsSpan()` into `plainTextBytes` without throwing exceptions on partial multibyte characters.
   * **Re-encode Base64 argument:**
     The truncated byte array is converted back to a Base64 string with padding stripped (`encodedString`).
   * **Rebuild Toast:**
     `ToastContentBuilder` is instantiated again with the updated, reduced `encodedString` argument, title, and body text.

### 5. Displaying the Notification
Finally, `toast.Show()` is called to dispatch the toast notification to the operating system notification center.