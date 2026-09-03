# Technical Documentation: `HdrToneMapper.cs`

**File Path:** `Text-Grab/Utilities/Hdr/HdrToneMapper.cs`  
**Namespace:** `Text_Grab.Utilities.Hdr`  
**Class:** `public static class HdrToneMapper`

---

## 1. Overview

The `HdrToneMapper` class is a `static` utility class containing pure, side-effect-free helper functions. Its primary purpose is to convert floating-point linear scRGB (FP16) channel values captured from an HDR display down to standard 8-bit gamma-corrected sRGB values (0–255).

### Problem Solved
When HDR is enabled on Windows, the desktop is composited in scRGB space using linear, unbounded color values:
- `1.0` in scRGB corresponds to the standard sRGB reference white level of **80 nits**.
- The Windows "SDR content brightness" slider boosts SDR white well above 80 nits, causing non-HDR content to be represented by scRGB values greater than `1.0`.

Without tone mapping, captured HDR screenshots retain this brightness boost and appear washed out or excessively bright. `HdrToneMapper` corrects this by scaling the channel values back based on the monitor's actual SDR white level, clipping HDR highlights above SDR white, and applying the standard sRGB transfer function.

---

## 2. Constants

### `SdrReferenceWhiteNits`
```csharp
public const double SdrReferenceWhiteNits = 80.0;
```
* **Type:** `double`
* **Description:** Represents the baseline sRGB reference white value in nits (80.0 nits). In scRGB color space, a value of `1.0` corresponds to this brightness level.

---

## 3. Method Details

### 3.1 `SdrWhiteScaleFromNits`

Converts a display's measured or reported SDR white level (in nits) into a scaling factor representing the scRGB value where SDR white resides.

```csharp
public static double SdrWhiteScaleFromNits(double sdrWhiteNits)
```

#### Parameters
* **`sdrWhiteNits`** (`double`): The target display's SDR white level in nits.

#### Return Value
* **`double`**: A scale factor $\ge 1.0$.

#### Behavior & Logic
1. Evaluates `sdrWhiteNits`: If `sdrWhiteNits` is less than or equal to `0`, it defaults to `SdrReferenceWhiteNits` (80.0).
2. Calculates `Math.Max(nits, SdrReferenceWhiteNits) / SdrReferenceWhiteNits`.
3. Clamps the scale to never drop below `1.0` (80 nits), ensuring the image is scaled down or kept equal, but never artificially brightened.

---

### 3.2 `ScRgbChannelToSrgbByte`

Maps a single linear scRGB channel value to an 8-bit sRGB byte value (`0` to `255`).

```csharp
public static byte ScRgbChannelToSrgbByte(double channel, double sdrWhiteScale)
```

#### Parameters
* **`channel`** (`double`): The raw, linear scRGB color channel value. This value can be negative (for out-of-sRGB-gamut colors) or greater than `1.0` (for HDR highlights).
* **`sdrWhiteScale`** (`double`): The SDR white scale factor calculated by `SdrWhiteScaleFromNits`.

#### Return Value
* **`byte`**: An 8-bit integer representing the final color channel value in the range `[0, 255]`.

#### Behavior & Logic
1. **Fallback Check:** If `sdrWhiteScale <= 0`, it defaults `sdrWhiteScale` to `1.0`.
2. **Normalization & Clamping:**
   $$\text{normalized} = \text{Clamp}\left(\frac{\text{channel}}{\text{sdrWhiteScale}}, 0.0, 1.0\right)$$
   * Division by `sdrWhiteScale` scales SDR white back to `1.0`.
   * Negative values (out-of-gamut) clip to `0.0`.
   * Values exceeding SDR white (HDR highlights) clip to `1.0`.
3. **Gamma Transfer Function:** Applies `LinearToSrgb(normalized)` to encode the linear float into non-linear sRGB space.
4. **Quantization:** Multiplies the resulting sRGB float by `255.0`, adds `0.5` for rounding, and clamps the integer result to the range `[0, 255]`.

---

### 3.3 `LinearToSrgb`

Applies the standard sRGB opto-electronic transfer function (OETF) to convert a linear normalized color value in the range $[0.0, 1.0]$ into non-linear sRGB space.

```csharp
public static double LinearToSrgb(double value)
```

#### Parameters
* **`value`** (`double`): A normalized linear color channel value in $[0.0, 1.0]$.

#### Return Value
* **`double`**: The gamma-encoded sRGB channel value.

#### Formula Breakdown
The function executes the standard piecewise sRGB transfer function:

$$\text{sRGB}(L) = \begin{cases} 12.92 \times L, & \text{if } L \le 0.0031308 \\ 1.055 \times L^{\left(\frac{1}{2.4}\right)} - 0.055, & \text{if } L > 0.0031308 \end{cases}$$

where $L$ is the input parameter `value`.

---

## 4. Processing Pipeline Flow

When converting an HDR channel value to an SDR byte representation using this class, the processing steps proceed as follows:

```
[ Display SDR Brightness (Nits) ]
              │
              ▼
  SdrWhiteScaleFromNits() ──► Produces sdrWhiteScale (>= 1.0)
                                      │
[ Raw Linear scRGB Channel ]          │
              │                       │
              ▼                       ▼
      ScRgbChannelToSrgbByte(channel, sdrWhiteScale)
              │
              ├─► Divide channel by sdrWhiteScale
              ├─► Clamp to [0.0, 1.0]
              ├─► LinearToSrgb() [Applies sRGB Transfer Function]
              ├─► Multiply by 255.0 + 0.5 (Rounding)
              └─► Clamp to [0, 255] cast to byte
                                      │
                                      ▼
                        [ 8-Bit sRGB Byte Output ]
```