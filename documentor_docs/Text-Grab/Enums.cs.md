# Technical Documentation: `Text-Grab/Enums.cs`

## Overview

The `Text-Grab/Enums.cs` file defines a central collection of enumerations (`enum`) within the `Text_Grab` namespace. These enumerations provide strongly typed options and identifiers for various application settings, user interface modes, Optical Character Recognition (OCR) options, input configurations, file handling, and UI selection behaviors.

---

## File Details

* **File Path:** `Text-Grab/Enums.cs`
* **Namespace:** `Text_Grab`

---

## Enumerations Reference

Below is a detailed reference of every enumeration declared in `Enums.cs`, including member names, integer/hex values, underlying types, and inline source comments.

---

### 1. `AddRemove`
Represents binary add or remove operations.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Add` | `0` | Addition operation |
| `Remove` | `1` | Removal operation |

---

### 2. `AppTheme`
Defines the visual theme choices for the application.

| Member | Value | Description |
| :--- | :--- | :--- |
| `System` | `0` | Follow system theme settings |
| `Dark` | `1` | Enforce dark theme |
| `Light` | `2` | Enforce light theme |

---

### 3. `CurrentCase`
Specifies text casing states.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Lower` | `0` | Lowercase text |
| `Camel` | `1` | CamelCase text |
| `Upper` | `2` | Uppercase text |
| `Unknown` | `3` | Unspecified or unrecognized case |

---

### 4. `FileStorageKind`
Defines the strategy or location type used for storing files.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Absolute` | `0` | Absolute path storage |
| `WithExe` | `1` | Stored relative to the application executable |
| `WithHistory` | `2` | Stored within application history |

---

### 5. `OpenContentKind`
Specifies the type of content being opened or handled.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Image` | `0` | Image file |
| `TextFile` | `1` | Plain text file |
| `Directory` | `2` | File directory/folder |
| `PdfDocument` | `3` | PDF document |

---

### 6. `OcrEngineKind`
Specifies the OCR engine used for text recognition.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Windows` | `0` | Native Windows OCR engine |
| `Tesseract` | `1` | Tesseract OCR engine |

---

### 7. `OcrOutputKind`
Specifies the target structure or output granularity of OCR processing.

| Member | Value | Description |
| :--- | :--- | :--- |
| `None` | `0` | No output |
| `Line` | `1` | Line-by-line output |
| `Paragraph` | `2` | Paragraph-level output |
| `Barcode` | `3` | Barcode reading output |

---

### 8. `Side`
Represents spatial directional sides or alignments.

| Member | Value | Description |
| :--- | :--- | :--- |
| `None` | `0` | Unspecified side |
| `Left` | `1` | Left side |
| `Right` | `2` | Right side |
| `Top` | `3` | Top side |
| `Bottom` | `4` | Bottom side |

---

### 9. `SpotInLine`
Specifies a positioning spot within a line of text.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Beginning` | `0` | Start of the line |
| `End` | `1` | End of the line |

---

### 10. `TextGrabMode`
Defines the main operating modes for Text-Grab features.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Fullscreen` | `0` | Fullscreen capture mode |
| `GrabFrame` | `1` | Grab Frame capture mode |
| `EditText` | `2` | Text editing window mode |
| `QuickLookup` | `3` | Quick lookup mode |

---

### 11. `VirtualKeyCodes`
* **Underlying Type:** `short`
* Maps virtual key codes for specific mouse buttons.

| Member | Value (Hex) | Value (Decimal) | Description |
| :--- | :--- | :--- | :--- |
| `LeftButton` | `0x01` | `1` | Left mouse button virtual key code |
| `RightButton` | `0x02` | `2` | Right mouse button virtual key code |
| `MiddleButton` | `0x04` | `4` | Middle mouse button virtual key code |

---

### 12. `ScrollBehavior`
Defines response actions when a scroll gesture occurs.

| Member | Value | Description |
| :--- | :--- | :--- |
| `None` | `0` | Scrolling causes no action |
| `Resize` | `1` | Scrolling resizes the target element |
| `Zoom` | `2` | Scrolling zooms the target element |
| `ZoomWhenFrozen` | `3` | Scrolling zooms only when in a frozen state |

---

### 13. `GrabFrameBorderStyle`
Controls the visual rendering style of the border in Grab Frame mode.

| Member | Value | Source Comment / Behavior |
| :--- | :--- | :--- |
| `Theme` | `0` | Follow the app light/dark theme (default behavior). |
| `HighContrast` | `1` | Two-tone white+black border; one tone always contrasts with any background. |
| `Color` | `2` | A fixed user-picked color. |

---

### 14. `SpellCheckMode`
Configures spell checking behaviors.

| Member | Value | Source Comment / Behavior |
| :--- | :--- | :--- |
| `Auto` | `0` | Enable spell check unless the text looks like it would choke the checker (very long documents or several long unspaced tokens). |
| `AlwaysOn` | `1` | Always show spell check, regardless of content. |
| `Off` | `2` | Never show spell check. |

---

### 15. `LanguageKind`
Identifies sources or engines associated with language models and accessibility automation.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Global` | `0` | Global language context |
| `Tesseract` | `1` | Tesseract engine language context |
| `WindowsAi` | `2` | Windows AI language context |
| `UiAutomation` | `3` | UI Automation language context |
| `WindowsAiDescription` | `4` | Windows AI Description context |

---

### 16. `UiAutomationTraversalMode`
Specifies performance/depth modes for UI Automation tree traversal.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Fast` | `0` | Fast, low-depth traversal |
| `Balanced` | `1` | Balanced traversal speed and depth |
| `Thorough` | `2` | Deep, exhaustive traversal |

---

### 17. `FsgDefaultMode`
Defines default operating modes for Full Screen Grab (FSG).

| Member | Value | Description |
| :--- | :--- | :--- |
| `Default` | `0` | Standard default selection mode |
| `SingleLine` | `1` | Single line grab mode |
| `Table` | `2` | Table grid capture mode |

---

### 18. `FsgSelectionStyle`
Specifies selection region styles in Full Screen Grab (FSG).

| Member | Value | Description |
| :--- | :--- | :--- |
| `Region` | `0` | Rectangular region selection |
| `Window` | `1` | Target window selection |
| `Freeform` | `2` | Freeform lasso selection |
| `AdjustAfter` | `3` | Post-selection adjustment mode |