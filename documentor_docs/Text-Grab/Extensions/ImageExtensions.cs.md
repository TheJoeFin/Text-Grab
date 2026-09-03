# Technical Documentation: `Text-Grab/Extensions/ImageExtensions.cs`

## Overview

The `ImageExtensions` class is an internal static utility class in the `Text_Grab.Extensions` namespace. It provides extension methods for the `System.Drawing.Image` class to inspect and adjust an image's orientation based on its embedded EXIF metadata tags.

Its primary purpose is to:
1. Extract EXIF orientation metadata from an `Image` instance.
2. Determine the appropriate `RotateFlipType` transformation required to display the image correctly.
3. Apply the rotation/flip operation directly to the image and strip the EXIF orientation metadata tag once corrected.

---

## Class Information

* **Namespace:** `Text_Grab.Extensions`
* **Access Modifier:** `internal`
* **Class Type:** `static`
* **Dependencies:**
  * `System`
  * `System.Drawing`
  * `System.Drawing.Imaging`
  * `System.Linq`

---

## Constants

### `exifOrientationID`
* **Type:** `private const int`
* **Value:** `0x112` (Decimal: `274`)
* **Description:** Represents the standard EXIF property tag ID used for orientation metadata.

---

## Methods

### `ExifRotate(this Image img)`

Applies the necessary rotation and flipping transformations to the provided `Image` object according to its EXIF orientation tag, then removes the tag.

#### Signature
```csharp
internal static void ExifRotate(this Image img)
```

#### Parameters
* `img` (`Image`): The extended `System.Drawing.Image` instance to rotate and strip metadata from.

#### Logic Flow
1. Calls `GetRotateFlipType(img)` to determine if a transformation is needed.
2. If the resulting `RotateFlipType` is not `RotateFlipType.RotateNoneFlipNone`:
   * Calls `img.RotateFlip(rot)` to transform the image.
   * Calls `img.RemovePropertyItem(exifOrientationID)` to remove the EXIF orientation metadata property item from the image.

---

### `GetRotateFlipType(this Image img)`

Inspects the EXIF metadata of the given image to derive the corresponding `System.Drawing.RotateFlipType`.

#### Signature
```csharp
internal static RotateFlipType GetRotateFlipType(this Image img)
```

#### Parameters
* `img` (`Image`): The extended `System.Drawing.Image` instance to evaluate.

#### Return Value
* `RotateFlipType`: The calculated rotation/flip enum value corresponding to the image's EXIF orientation. Returns `RotateFlipType.RotateNoneFlipNone` if:
  * The image does not contain the EXIF orientation property ID (`0x112`).
  * The property item cannot be retrieved.
  * The property value is not a valid byte array.

#### Logic Flow
1. **Validation & Extraction:**
   * Checks if `img.PropertyIdList` contains `0x112`.
   * Attempts to retrieve `PropertyItem` via `img.GetPropertyItem(0x112)`.
   * Verifies `prop.Value` is a `byte[]`.
   * Returns `RotateFlipType.RotateNoneFlipNone` if any of these checks fail.
2. **Value Parsing:**
   * Converts the byte array to a 16-bit unsigned integer (`ushort`) value `val` starting at index 0 via `BitConverter.ToUInt16(propValue, 0)`.
3. **Rotation Calculation:**
   * Initializes `rot` to `RotateFlipType.RotateNoneFlipNone`.
   * Sets `rot` based on `val`:
     * `val` is `3` or `4`: `RotateFlipType.Rotate180FlipNone`
     * `val` is `5` or `6`: `Rotate90FlipNone`
     * `val` is `7` or `8`: `Rotate270FlipNone`
4. **Horizontal Flip Calculation:**
   * If `val` is `2`, `4`, `5`, or `7`, performs a bitwise OR operation on `rot`: `rot |= RotateFlipType.RotateNoneFlipX`.
5. **Return:**
   * Returns the final `RotateFlipType`.

---

## EXIF Orientation Value Mapping Reference

The following table summarizes how raw EXIF orientation values map to `RotateFlipType` values within `GetRotateFlipType`:

| EXIF Value (`val`) | Base Rotation | Horizontal Flip Added (`|= RotateNoneFlipX`) | Final `RotateFlipType` Result |
| :--- | :--- | :--- | :--- |
| `1` | `RotateNoneFlipNone` | No | `RotateNoneFlipNone` |
| `2` | `RotateNoneFlipNone` | Yes | `RotateNoneFlipX` |
| `3` | `Rotate180FlipNone` | No | `Rotate180FlipNone` |
| `4` | `Rotate180FlipNone` | Yes | `Rotate180FlipX` |
| `5` | `Rotate90FlipNone` | Yes | `Rotate90FlipX` |
| `6` | `Rotate90FlipNone` | No | `Rotate90FlipNone` |
| `7` | `Rotate270FlipNone` | Yes | `Rotate270FlipX` |
| `8` | `Rotate270FlipNone` | No | `Rotate270FlipNone` |
| *Other / Missing* | `RotateNoneFlipNone` | No | `RotateNoneFlipNone` |