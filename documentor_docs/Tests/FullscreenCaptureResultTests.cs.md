# Technical Documentation: `FullscreenCaptureResultTests.cs`

## Overview

The `FullscreenCaptureResultTests` class is a unit test suite within the `Tests` namespace. Its primary purpose is to verify the behavior of properties on the `FullscreenCaptureResult` model—specifically `SupportsTemplateActions` and `SupportsPreviousRegionReplay`—based on different selection styles (`FsgSelectionStyle`).

---

## File Information

* **File Path:** `Tests/FullscreenCaptureResultTests.cs`
* **Namespace:** `Tests`
* **Dependencies:**
  * `System.Windows` (Provides `Rect`)
  * `Text_Grab`
  * `Text_Grab.Models` (Provides `FsgSelectionStyle`, `FullscreenCaptureResult`)

---

## Class Overview

### `FullscreenCaptureResultTests`

A public unit test class containing parameterized xUnit tests (`[Theory]`) designed to validate property logic in `FullscreenCaptureResult`.

---

## Test Methods

### 1. `SupportsTemplateActions_MatchesSelectionStyle`

#### Description
Validates that the `SupportsTemplateActions` boolean property on a `FullscreenCaptureResult` instance correctly evaluates to `true` or `false` depending on the `FsgSelectionStyle` passed to the constructor.

#### Signature
```csharp
[Theory]
[InlineData(FsgSelectionStyle.Region, true)]
[InlineData(FsgSelectionStyle.Window, true)]
[InlineData(FsgSelectionStyle.Freeform, false)]
[InlineData(FsgSelectionStyle.AdjustAfter, true)]
public void SupportsTemplateActions_MatchesSelectionStyle(FsgSelectionStyle selectionStyle, bool expected)
```

#### Test Data Mapping

| `FsgSelectionStyle` | Expected `SupportsTemplateActions` Value |
| :--- | :--- |
| `FsgSelectionStyle.Region` | `true` |
| `FsgSelectionStyle.Window` | `true` |
| `FsgSelectionStyle.Freeform` | `false` |
| `FsgSelectionStyle.AdjustAfter` | `true` |

#### How It Works
1. Instantiates a `FullscreenCaptureResult` object using the given `selectionStyle` parameter and `Rect.Empty` for the capture bounds.
2. Asserts that `result.SupportsTemplateActions` matches the `expected` boolean value.

---

### 2. `SupportsPreviousRegionReplay_MatchesSelectionStyle`

#### Description
Validates that the `SupportsPreviousRegionReplay` boolean property on a `FullscreenCaptureResult` instance correctly evaluates to `true` or `false` depending on the `FsgSelectionStyle` passed to the constructor.

#### Signature
```csharp
[Theory]
[InlineData(FsgSelectionStyle.Region, true)]
[InlineData(FsgSelectionStyle.Window, false)]
[InlineData(FsgSelectionStyle.Freeform, false)]
[InlineData(FsgSelectionStyle.AdjustAfter, true)]
public void SupportsPreviousRegionReplay_MatchesSelectionStyle(FsgSelectionStyle selectionStyle, bool expected)
```

#### Test Data Mapping

| `FsgSelectionStyle` | Expected `SupportsPreviousRegionReplay` Value |
| :--- | :--- |
| `FsgSelectionStyle.Region` | `true` |
| `FsgSelectionStyle.Window` | `false` |
| `FsgSelectionStyle.Freeform` | `false` |
| `FsgSelectionStyle.AdjustAfter` | `true` |

#### How It Works
1. Instantiates a `FullscreenCaptureResult` object using the given `selectionStyle` parameter and `Rect.Empty` for the capture bounds.
2. Asserts that `result.SupportsPreviousRegionReplay` matches the `expected` boolean value.

---

## Summary of Tested Behaviors

| Selection Style (`FsgSelectionStyle`) | `SupportsTemplateActions` | `SupportsPreviousRegionReplay` |
| :--- | :---: | :---: |
| `Region` | `true` | `true` |
| `Window` | `true` | `false` |
| `Freeform` | `false` | `false` |
| `AdjustAfter` | `true` | `true` |