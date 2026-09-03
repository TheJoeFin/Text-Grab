# Technical Documentation: `OcrOutput.cs`

**File Path:** `Text-Grab/Models/OcrOutput.cs`  
**Namespace:** `Text_Grab.Models`  
**Type:** `public record OcrOutput`

---

## Overview

The `OcrOutput` record is a data model used within Text-Grab to encapsulate the results and context of an Optical Character Recognition (OCR) operation. It stores details such as the engine used, output classification type, raw and post-processed text results, references to source image bitmaps, and the language context. Additionally, it contains logic to sanitize and post-process the extracted raw text based on user settings.

---

## Class Definition

```csharp
public record OcrOutput
```

`OcrOutput` is defined as a C# positional reference record, enabling immutable-style record features while providing mutable properties with default initializers.

---

## Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Engine` | `OcrEngineKind` | `OcrEngineKind.Windows` | Specifies the OCR engine responsible for generating the output. |
| `Kind` | `OcrOutputKind` | `OcrOutputKind.None` | Classifies the type/category of the OCR output (e.g., Barcode, None, etc.). |
| `RawOutput` | `string` | `string.Empty` | Holds the unmodified string output produced directly by the OCR engine. |
| `CleanedOutput` | `string` | `string.Empty` | Holds the processed text result after applying cleaning and correction logic. |
| `SourceBitmap` | `Bitmap?` | `null` | A nullable `System.Drawing.Bitmap` reference representing the source image processed by the OCR engine. |
| `SourceSoftwareBitmap` | `SoftwareBitmap?` | `null` | A nullable `Windows.Graphics.Imaging.SoftwareBitmap` reference representing the source image for Windows RT/UWP image pipelines. |
| `Language` | `ILanguage?` | `null` | An object implementing `ILanguage` representing the language context used during OCR execution. |

---

## Methods

### `CleanOutput()`

```csharp
public void CleanOutput()
```

#### Purpose
`CleanOutput()` processes `RawOutput` according to the application's current user settings and language parameters, updating `CleanedOutput` with the post-processed result.

#### Execution Logic & Steps

1. **Early Exit Validation**:
   * Evaluates if `AppUtilities.TextGrabSettings` cannot be cast to `Settings userSettings`.
   * Evaluates if `Kind` is equal to `OcrOutputKind.Barcode`.
   * If either condition is true, the method aborts execution early and makes no changes to `CleanedOutput`.

2. **Initialization**:
   * Copies `RawOutput` into a local working variable `correctingString`.

3. **Latin Character Correction**:
   * Checks if `userSettings.CorrectToLatin` is `true` AND `Language?.IsLatinBased()` returns `true`.
   * If true, transforms `correctingString` by invoking the extension method `.ReplaceGreekOrCyrillicWithLatin()`.

4. **Error Correction**:
   * Checks if `userSettings.CorrectErrors` is `true`.
   * If true, transforms `correctingString` by invoking the extension method `.TryFixEveryWordLetterNumberErrors()`.

5. **Assignment**:
   * Assigns the final value of `correctingString` to the `CleanedOutput` property.

---

## Dependencies & Imports

* **`System.Drawing`**: Provides the `Bitmap` class.
* **`Windows.Graphics.Imaging`**: Provides the `SoftwareBitmap` class.
* **`Text_Grab.Interfaces`**: Provides the `ILanguage` interface.
* **`Text_Grab.Properties`**: Provides access to `Settings`.
* **`Text_Grab.Utilities`**: Provides utility classes and extension methods (`AppUtilities`, `ReplaceGreekOrCyrillicWithLatin()`, `TryFixEveryWordLetterNumberErrors()`, `IsLatinBased()`).