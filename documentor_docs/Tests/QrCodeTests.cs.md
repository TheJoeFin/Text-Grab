# Technical Documentation: `Tests/QrCodeTests.cs`

## Overview

The `Tests/QrCodeTests.cs` file contains automated unit tests for validating QR code generation logic within the `Text_Grab` application. Specifically, it tests the SVG QR code generation method provided by the `BarcodeUtilities` utility class using the xUnit testing framework.

---

## File Details

* **File Path:** `Tests/QrCodeTests.cs`
* **Namespace:** `Tests`
* **Test Framework:** xUnit (`[Fact]`, `Assert`)

---

## Imports & Dependencies

| Namespace | Source / Purpose |
| :--- | :--- |
| `Text_Grab.Utilities` | Provides access to `BarcodeUtilities`, which contains the barcode/QR code helper methods. |
| `ZXing.QrCode.Internal` | Provides `ErrorCorrectionLevel` enum used to specify the QR code error correction level. |

---

## Class Structure

### `public class QrCodeTests`

A public unit test class that encapsulates test cases for QR code generation functionality.

---

## Test Methods

### `generateSvgImage()`

* **Attribute:** `[Fact]`
* **Return Type:** `void`
* **Purpose:** Verifies that passing a standard text string and an error correction level to `BarcodeUtilities.GetSvgQrCodeForText` successfully returns a non-null `ZXing.Rendering.SvgRenderer.SvgImage` object.

#### Method Workflow:
1. **Arrange:**
   * Defines a local test input string:
     ```csharp
     string testString = "This is only a test";
     ```
2. **Act:**
   * Calls `BarcodeUtilities.GetSvgQrCodeForText(testString, ErrorCorrectionLevel.L)` to generate an SVG representation of the QR code.
   * Stores the returned object as a `ZXing.Rendering.SvgRenderer.SvgImage` type.
3. **Assert:**
   * Executes `Assert.NotNull(svg)` to verify that the generated SVG image object is not `null`.

---

## Code Summary

```csharp
using Text_Grab.Utilities;
using ZXing.QrCode.Internal;

namespace Tests;

public class QrCodeTests
{
    [Fact]
    public void generateSvgImage()
    {
        string testString = "This is only a test";
        ZXing.Rendering.SvgRenderer.SvgImage svg = BarcodeUtilities.GetSvgQrCodeForText(testString, ErrorCorrectionLevel.L);

        Assert.NotNull(svg);
    }
}
```