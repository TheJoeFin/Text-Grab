# Overview: `Tests/HdrToneMapperTests.cs`

The `HdrToneMapperTests` class contains unit tests written in xUnit to verify the correct behavior of the `HdrToneMapper` utility class (located in the `Text_Grab.Utilities.Hdr` namespace). 

The test suite ensures that SDR white scaling, linear-to-sRGB transfer conversions, and scRGB channel conversions to sRGB byte values operate within expected bounds and adhere to mathematical and color space constraints.

---

## Namespace & Imports

```csharp
using Text_Grab.Utilities.Hdr;

namespace Tests;
```

- **Namespace**: `Tests`
- **Dependencies**: Uses `Text_Grab.Utilities.Hdr` for `HdrToneMapper` and xUnit testing attributes (`[Fact]`, `[Theory]`, `[InlineData]`).

---

## Test Cases Breakdown

### 1. `SdrWhiteScaleFromNits` Tests

These tests validate the calculation of the scaling factor derived from display brightness measured in nits (cd/m²), using **80.0 nits** as the baseline reference white point.

#### `SdrWhiteScaleFromNits_ReferenceWhite_IsOne`
* **Type**: `[Fact]`
* **Purpose**: Verifies that a display SDR white level of `80.0` nits yields a scale factor of exactly `1.0`.
* **Assertion**: `Assert.Equal(1.0, HdrToneMapper.SdrWhiteScaleFromNits(80.0), 5)` (precision to 5 decimal places).

#### `SdrWhiteScaleFromNits_ScalesRelativeTo80Nits`
* **Type**: `[Theory]`
* **Data Sources**:
  * `200.0` nits $\rightarrow$ expected scale: `2.5`
  * `160.0` nits $\rightarrow$ expected scale: `2.0`
  * `480.0` nits $\rightarrow$ expected scale: `6.0`
* **Purpose**: Confirms that nit values above 80 scale linearly relative to the 80-nit reference ($nits / 80.0$).

#### `SdrWhiteScaleFromNits_NeverBrightens`
* **Type**: `[Theory]`
* **Data Sources**: `0.0`, `-5.0`, `40.0`
* **Purpose**: Guarantees that brightness levels at or below the 80-nit reference level do not produce a scale factor less than `1.0`. This prevents unintended image brightening during tone mapping.
* **Assertion**: Expects `1.0` output for all inputs $\le 80.0$.

---

### 2. `LinearToSrgb` Tests

#### `LinearToSrgb_Endpoints`
* **Type**: `[Fact]`
* **Purpose**: Validates the boundary points of the linear-to-sRGB conversion function.
* **Assertions**:
  * `LinearToSrgb(0.0)` must equal `0.0`
  * `LinearToSrgb(1.0)` must equal `1.0`

---

### 3. `ScRgbChannelToSrgbByte` Tests

These tests evaluate the conversion of high-dynamic-range scRGB float channel values into 8-bit sRGB byte channels (`0–255`), using scaling derived from SDR white nits.

#### `ScRgbChannelToSrgbByte_SdrWhiteMapsToFullWhite`
* **Type**: `[Fact]`
* **Setup**: `scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0)` (yields `2.5`).
* **Purpose**: Tests that an scRGB channel value equal to SDR white (2.5 at 200 nits) maps directly to full white (`255`).
* **Assertion**: `Assert.Equal(255, HdrToneMapper.ScRgbChannelToSrgbByte(2.5, scale))`

#### `ScRgbChannelToSrgbByte_UndoesHdrBrightnessBoost`
* **Type**: `[Fact]`
* **Setup**: `scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0)`
* **Purpose**: Ensures mid-gray content in scRGB linear light (`1.25`—half of SDR white `2.5`) properly scales down into expected sRGB byte ranges rather than over-brightening.
* **Assertion**: Asserts that `midGray` falls in the range `[186, 190]` (specifically targeting $\approx 188$).

#### `ScRgbChannelToSrgbByte_HighlightsAboveSdrWhiteClipToWhite`
* **Type**: `[Fact]`
* **Setup**: `scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0)`
* **Purpose**: Verifies that HDR specular highlight channel values significantly exceeding SDR white (e.g., `10.0`) clip gracefully to `255` instead of overflowing or wrapping around.
* **Assertion**: Expects `255`.

#### `ScRgbChannelToSrgbByte_NegativeWideGamutClampsToBlack`
* **Type**: `[Fact]`
* **Setup**: `scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0)`
* **Purpose**: Confirms that out-of-gamut negative scRGB channel values (e.g., `-0.5`) clamp to `0` (black).
* **Assertion**: Expects `0`.

#### `ScRgbChannelToSrgbByte_IsMonotonic`
* **Type**: `[Fact]`
* **Setup**: Loops `channel` from `0.0` to `2.5` in increments of `0.05` using scale derived from `200.0` nits.
* **Purpose**: Asserts monotonicity across the full SDR conversion range—ensuring byte output values never decrease as scRGB channel inputs increase.
* **Assertion**: `Assert.True(value >= previous, ...)` for every iteration.

---

## Summary of Tested Contracts

Based on the test assertions, the `HdrToneMapper` component enforces the following rules:
1. **SDR Reference Baseline**: Uses $80\text{ nits}$ as standard white; values $\le 80$ nits map to a scale factor of $1.0$.
2. **Linear Nit Scaling**: Displays with elevated SDR white levels scale proportionally ($scale = nits / 80$).
3. **Conversion Limits**:
   * Values mapping at or above SDR white output `255`.
   * Negative linear/scRGB values clamp to `0`.
4. **Order Preservation**: Output conversion across linear increments is strictly monotonic.