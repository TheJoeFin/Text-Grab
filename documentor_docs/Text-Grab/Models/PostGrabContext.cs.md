# Technical Documentation: `PostGrabContext.cs`

**File Path:** `Text-Grab/Models/PostGrabContext.cs`  
**Namespace:** `Text_Grab.Models`  
**Type:** C# Positional Record (`public record PostGrabContext`)

---

## Overview

The `PostGrabContext` record serves as a data container carrying contextual information produced by a screen grab action. This data is passed through the post-grab action pipeline. It enables actions that require full capture metadata (such as capture coordinates, DPI, captured image, language, or selection style) to access that data, while also allowing actions that only require extracted text to function seamlessly.

---

## Record Signature and Dependencies

```csharp
namespace Text_Grab.Models;

public record PostGrabContext(
    string Text,
    Rect CaptureRegion,
    double DpiScale,
    BitmapSource? CapturedImage,
    ILanguage? Language = null,
    FsgSelectionStyle SelectionStyle = FsgSelectionStyle.Region
)
```

### Namespace Imports
- `System.Windows`: Provides the `Rect` struct used for defining capture region coordinates.
- `System.Windows.Media.Imaging`: Provides the `BitmapSource` class for bitmap images.
- `Text_Grab.Interfaces`: Provides interface definitions such as `ILanguage`.

---

## Primary Constructor Parameters / Properties

| Parameter | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| **`Text`** | `string` | *Required* | The OCR text extracted from the full capture region. |
| **`CaptureRegion`** | `Rect` | *Required* | The screen rectangle (in physical pixels) that was captured. Used for template execution to derive sub-region rectangles. |
| **`DpiScale`** | `double` | *Required* | The DPI scale factor recorded at capture time. |
| **`CapturedImage`** | `BitmapSource?` | *Required* | An optional in-memory copy (`BitmapSource`) of the captured image. Can be `null`. |
| **`Language`** | `ILanguage?` | `null` | The OCR language used for the capture. If `null`, indicates that the default application language should be used. |
| **`SelectionStyle`** | `FsgSelectionStyle` | `FsgSelectionStyle.Region` | The selection style used to produce the capture. |

---

## Static Methods

### `TextOnly`

```csharp
public static PostGrabContext TextOnly(string text)
```

**Purpose:**  
A convenience factory method for creating a `PostGrabContext` instance when only text is available or required (e.g., for non-template actions).

**Parameters:**
- `text` (`string`): The text string to populate the context with.

**Returns:**
- A new instance of `PostGrabContext` populated with:
  - `Text`: Provided `text` argument
  - `CaptureRegion`: `Rect.Empty`
  - `DpiScale`: `1.0`
  - `CapturedImage`: `null`
  - `Language`: `null`
  - `SelectionStyle`: `FsgSelectionStyle.Region`

---

## How It Works

1. **Context Data Container:** As an immutable C# `record`, `PostGrabContext` encapsulates the outputs and settings of an individual capture operation.
2. **Template & Sub-region Support:** Fields like `CaptureRegion` and `DpiScale` preserve spatial and scaling details from the capture, allowing template-based processing to re-run sub-region OCR operations accurately.
3. **Flexible Initialization:** Callers can either construct the full `PostGrabContext` record with custom parameters or call `PostGrabContext.TextOnly(text)` to generate a minimal context with standard defaults.