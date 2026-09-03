# Technical Documentation: `Text-Grab/Utilities/ImageChangeDetector.cs`

## Overview

The `ImageChangeDetector` class is a utility designed to detect significant visual changes across sequential screen captures or between two individual snapshots. To maximize performance and minimize memory allocations, it downscales incoming full-resolution images into tiny $96 \times 96$ thumbnails before performing image comparisons using the **Magick.NET** library (`ImageMagick`).

### Key Operational Concepts
1. **Performance Optimization via Downscaling**: Comparison operations are executed on small thumbnails ($96 \times 96$ pixels), rendering image checks computationally light regardless of the original screen capture dimensions.
2. **Noise Suppression (Thresholding)**: Small visual variances—such as antialiasing differences or blinking text carets—are filtered out using a normalized error threshold.
3. **Stability Verification**: Dynamic continuous checks require that a newly detected change remains stable across two consecutive samples before reporting a true change. This prevents temporary UI glitches, flashing indicators, or half-rendered frames from triggering false positives.

---

## Class Signature

```csharp
namespace Text_Grab.Utilities;

public sealed partial class ImageChangeDetector : IDisposable
```

* **Modifiers**: `sealed`, `partial`
* **Interfaces**: `IDisposable`

---

## Constants

| Constant | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `ComparisonSize` | `int` | `96` | Defines the width and height (96x96 pixels) to which source bitmaps are downscaled for comparison. |
| `ChangeThreshold` | `double` | `0.001` | The maximum `NormalizedMeanError` treated as non-significant noise (e.g., antialiasing or blinking cursors). Differences strictly greater than this value register as visual changes. |

---

## Private Instance Fields

| Field | Type | Description |
| :--- | :--- | :--- |
| `imageFactory` | `MagickImageFactory` | Read-only factory instance used to convert System.Drawing `Bitmap` thumbnails into Magick.NET `MagickImage` objects. |
| `baselineImage` | `MagickImage?` | Stores the downscaled baseline image against which new screen captures are compared. |
| `previousImage` | `MagickImage?` | Stores the immediate previous downscaled capture used to verify frame-to-frame stability. |

---

## Methods

### 1. `CheckForChangeAndUpdate`

Evaluates a new screen capture against the active baseline image.

```csharp
public bool CheckForChangeAndUpdate(Bitmap capture)
```

#### Parameters
* **`capture`** (`Bitmap`): The full-resolution image capture to check for changes.

#### Returns
* **`bool`**: `true` if the capture **differs from the baseline** AND is **stable** (matches the immediate previous capture within the change threshold). Returns `false` otherwise, or if the baseline is established during this call.

#### Detailed Logic & Workflow
1. **Thumbnail Generation**: Downscales `capture` into a temporary 96x96 `Bitmap` using `CreateThumbnail()`.
2. **MagickImage Conversion**: Converts the thumbnail into a `MagickImage` instance (`currentImage`). If conversion fails (`null`), returns `false`.
3. **Baseline Initialization**:
   * If `baselineImage` is currently `null` (after instantiation or a call to `Reset()`):
     * Assigns `baselineImage = currentImage`.
     * Cleans up and disposes any residual `previousImage`.
     * Returns `false` (establishing the new reference baseline).
4. **Change Evaluation**:
   * **`differsFromBaseline`**: Evaluates whether `baselineImage.Compare(currentImage).NormalizedMeanError > ChangeThreshold`.
   * **`isStable`**: Evaluates whether `previousImage` exists AND `previousImage.Compare(currentImage).NormalizedMeanError <= ChangeThreshold`.
5. **State Update**:
   * Disposes the previous `previousImage`.
   * Updates `previousImage = currentImage`.
6. **Result**: Returns `differsFromBaseline && isStable`.

---

### 2. `ImagesDifferBeyondThreshold` (Static)

Performs a one-shot comparison between two arbitrary `Bitmap` instances, completely independent of any instantiated class baseline state.

```csharp
public static bool ImagesDifferBeyondThreshold(
    Bitmap first, 
    Bitmap second, 
    double threshold = ChangeThreshold)
```

#### Parameters
* **`first`** (`Bitmap`): The primary image to compare.
* **`second`** (`Bitmap`): The secondary image to compare.
* **`threshold`** (`double`, optional): The `NormalizedMeanError` threshold above which images are declared different. Defaults to `ChangeThreshold` (`0.001`).

#### Returns
* **`bool`**: `true` if the images differ by more than `threshold`, or if a comparison `MagickImage` fails to construct; `false` if they are visually identical within the threshold limit.

#### Behavior Details
* Creates 96x96 thumbnails for both `first` and `second` bitmaps.
* Instantiates a local `MagickImageFactory`.
* If either thumbnail fails to produce a valid `MagickImage`, the method defaults to `true` (indicating a difference as a safe fallback path).
* Appropriately disposes of created thumbnail bitmaps and `MagickImage` instances.

---

### 3. `Reset`

Resets the internal detector state by clearing and disposing stored baseline and previous images.

```csharp
public void Reset()
```

#### Behavior
* Calls `.Dispose()` on `baselineImage` (if non-null) and sets `baselineImage = null`.
* Calls `.Dispose()` on `previousImage` (if non-null) and sets `previousImage = null`.
* The next call to `CheckForChangeAndUpdate()` will set a new baseline image.

---

### 4. `Dispose`

Implements `IDisposable.Dispose`. Calls `Reset()` to free unmanaged graphics resources held by `baselineImage` and `previousImage`.

```csharp
public void Dispose()
```

---

### 5. `CreateThumbnail` (Private Static)

Helper method responsible for downscaling source bitmaps.

```csharp
private static Bitmap CreateThumbnail(Bitmap source)
```

#### Parameters
* **`source`** (`Bitmap`): The source image to downscale.

#### Returns
* **`Bitmap`**: A new $96 \times 96$ pixel `Bitmap` in `PixelFormat.Format32bppArgb`.

#### Implementation Notes
* Constructs a target thumbnail of size `ComparisonSize` $\times$ `ComparisonSize` ($96 \times 96$).
* Uses System.Drawing `Graphics` with `InterpolationMode.HighQualityBilinear`. This interpolation mode acts as a prefilter during downscaling so that small on-screen visual details (such as word modifications) influence the target thumbnail instead of being skipped.

---

## Lifecycle & Memory Management

Because `ImageChangeDetector` handles unmanaged GDI+ (`Bitmap`, `Graphics`) and Magick.NET (`MagickImage`) objects:
* **Immediate Local Cleanups**: Short-lived intermediate objects (`Bitmap` thumbnails, transient `MagickImage` handles) are wrapped in `using` blocks or explicitly disposed within the comparison methods.
* **Retained Resources**: Long-lived instances (`baselineImage`, `previousImage`) are explicitly retained across calls to `CheckForChangeAndUpdate`.
* **Explicit Disposal**: Callers **must** call `Dispose()` or wrap `ImageChangeDetector` in a `using` block once the change detector is no longer required to avoid resource leaks.