# Technical Documentation: `UiTests/TextGrab.AutomationHost/MainWindow.xaml.cs`

## Overview

The `MainWindow.xaml.cs` file defines the primary fixture window for the `TextGrab.AutomationHost` project. It provides an automated testing host designed to expose various UI surfaces, text rendering scenarios, images, barcodes, and DPI configurations for automated UI and OCR (Optical Character Recognition) testing.

The window dynamically renders test surfaces, tracks metrics (such as DPI, bounds, and monitor details), and writes test execution events out to a state file using a `FixtureStateWriter`.

---

## Key Responsibilities

1. **Test Surface Rendering:** Hosts and switches between multiple predefined content surfaces designed to test specific extraction capabilities (e.g., standard text, native automation controls, multilingual text, OCR images, QR/barcodes, tables, empty regions, high-contrast, and coordinate grids).
2. **State & Event Logging:** Writes snapshot updates (`FixtureState`) containing current window metrics, input values, and selected surfaces whenever window states or surfaces change.
3. **Display Metrics & P/Invoke Integration:** Uses Win32 P/Invoke functions (`user32.dll`) to determine accurate window screen coordinates, display monitor information, and per-window DPI settings.

---

## Class Architecture & Members

### Class Declaration
```csharp
namespace TextGrab.AutomationHost;

public partial class MainWindow : Window
```

---

## Constants & Fields

| Member | Type | Description |
| :--- | :--- | :--- |
| `DefaultKnownText` | `const string` | Default fallback English test string spanning two lines. |
| `DefaultMultilingualText` | `const string` | Multilingual test string containing English, Arabic, Hebrew, Japanese, Chinese, and Korean text. |
| `options` | `FixtureOptions` | Read-only configuration options passed to the window during initialization. |
| `stateWriter` | `FixtureStateWriter` | Writer responsible for outputting state change logs to a state file path. |
| `coordinateDpiReadout` | `TextBlock?` | Optional reference to a text block updated with DPI/metric text when the `CoordinateDpi` surface is displayed. |
| `selectedSurface` | `string` | The identifier string of the currently active surface (defaults to `"KnownText"`). |
| `displayedText` | `string` | Holds the text rendered across test surfaces (defaults to `options.DisplayText` if provided, otherwise `DefaultKnownText`). |
| `OcrSamples` | `OcrSample[]` | Static array containing predefined test image descriptors (Name, FilePath, ExpectedText). |
| `Code39Encodings` | `IReadOnlyDictionary<char, int>` | Bitmask dictionary defining Code 39 barcode encoding patterns. |

---

## Constructor

### `MainWindow(FixtureOptions options)`
* **Purpose:** Initializes the window components, configures state logging, populates initial inputs, and sets the starting surface.
* **Logic:**
  1. Assigns `options` field and initializes `stateWriter` with `options.StateFile`.
  2. Calls `InitializeComponent()`.
  3. Sets `displayedText` from `options.DisplayText` (falling back to `DefaultKnownText` if blank).
  4. Sets the input field (`SurfaceTextInput.Text`) to `displayedText`.
  5. Selects the surface matching `options.Surface`.

---

## Event Handlers

### Window Event Handlers

* **`Window_Loaded(object sender, RoutedEventArgs e)`**
  * Displays the initial surface via `ShowSurface()`.
  * Logs the `"ready"` state via `UpdateWindowState()`.
  * Sets focus to `InputTarget`.

* **`Window_Activated(object? sender, EventArgs e)`**
  * Triggers state update with event name `"activated"`.

* **`Window_Changed(object sender, EventArgs e)`**
  * Triggers state update with event name `"window-changed"`.

### UI Interactive Controls

* **`SurfaceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)`**
  * Updates `selectedSurface` to the newly selected item string.
  * If the window is loaded, calls `ShowSurface()` and logs `"surface-changed"`.

* **`ShowSelectedSurface_Click(object sender, RoutedEventArgs e)`**
  * Rerenders the active surface via `ShowSurface()` and logs `"surface-shown"`.

* **`UpdateContent_Click(object sender, RoutedEventArgs e)`**
  * Sets `displayedText` from `SurfaceTextInput.Text`.
  * Refreshes surface and logs `"display-text-updated"`.

* **`ResetWindowBounds_Click(object sender, RoutedEventArgs e)`**
  * Resets window bounds to fixed coordinates:
    * `Left`: 100
    * `Top`: 100
    * `Width`: 1000
    * `Height`: 780
  * Logs state as `"bounds-reset"`.

* **`InputTarget_TextChanged(object sender, TextChangedEventArgs e)`**
  * Mirrors text from `InputTarget` to `ReceivedText.Text`.
  * If loaded, logs `"input-changed"`.

* **`ClearInput_Click(object sender, RoutedEventArgs e)`**
  * Clears `InputTarget`, focuses it, and logs `"input-cleared"`.

---

## Test Surface Generators

`ShowSurface()` dynamically populates `SurfaceContent.Content` based on `selectedSurface`. Below are the available surface generation methods:

| Surface Name | Method | Description / Rendered Components |
| :--- | :--- | :--- |
| **`KnownText`** | `CreateKnownTextSurface()` | Renders standard text blocks and read-only text boxes using `displayedText`. |
| **`DirectText`** | `CreateDirectTextSurface()` | Exposes native UI Automation controls: a read-only `TextBox`, a read-only `RichTextBox`, and an editable `TextBox`. |
| **`Multilingual`** | `CreateMultilingualSurface()` | Displays CJK, Arabic, and Hebrew text. Configures a Right-to-Left (RTL) text block (`FlowDirection.RightToLeft`). |
| **`OcrSamples`** | `CreateOcrSamplesSurface()` | Provides a ComboBox selector to cycle through static OCR test images loaded from the `Images/` folder, updating expected text descriptions. |
| **`QrBarcode`** | `CreateQrBarcodeSurface()` | Displays `QrCodeTestImage.png` alongside a procedurally generated Code 39 barcode (`TEXT-GRAB-123`). |
| **`Table`** | `CreateTableSurface()` | Constructs a 3x3 WPF `Grid` displaying tabular data ("Item", "Quantity", "Price") and loads `Table-Test.png`. |
| **`Empty`** | `CreateEmptySurface()` | Renders an empty white `Border` region with no text, intended for testing negative capture cases. |
| **`Contrast`** | `CreateContrastSurface()` | Displays current high-contrast system status (`SystemParameters.HighContrast`) and color samples (Black on White, White on Black, Yellow on Black, Cyan on Navy). |
| **`CoordinateDpi`** | `CreateCoordinateDpiSurface()` | Renders a 700x350 Canvas with gridlines spaced every 50 device-independent pixels and a live metric readout. |

---

## Helper & Utility Methods

### Barcode & Image Generation

* **`CreateBarcode()`**
  * Procedurally renders a Code 39 barcode onto a `Canvas` representing the string `*TEXT-GRAB-123*`.
  * Iterates through characters, reads encoding values from `Code39Encodings`, and draws black `Rectangle` elements of varying widths (2 or 4 pixels).

* **`LoadFixtureImage(string fileName)`**
  * Constructs an absolute URI to an image within `AppContext.BaseDirectory/Images/<fileName>` and returns a `BitmapImage`.

### Metric Calculation & P/Invoke State Output

* **`UpdateWindowState(string eventName)`**
  * Calculates current window bounds, monitor metadata, and DPI via `GetWindowMetrics()`.
  * Updates `WindowMetricsText` and `coordinateDpiReadout` text fields.
  * Writes state via `stateWriter.Write()` with a new `FixtureState` snapshot.

* **`GetWindowMetrics()`**
  * Retrieves native `HWND` using `WindowInteropHelper`.
  * Converts `(0,0)` window origin to screen coordinates via `PointToScreen`.
  * Obtains window DPI using P/Invoke `GetDpiForWindow` (falls back to WPF `VisualTreeHelper.GetDpi`).
  * Resolves monitor details using P/Invoke `MonitorFromWindow` and `GetMonitorInfo`.
  * Returns tuple `(string Bounds, string Monitor, uint Dpi)`.

---

## Native Interoperability (P/Invoke) & Structs

The file imports three functions from `user32.dll` to retrieve low-level system metrics:

```csharp
[DllImport("user32.dll")]
private static extern uint GetDpiForWindow(IntPtr hwnd);

[DllImport("user32.dll")]
private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);
```

### Native Struct
```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
private struct MonitorInfoEx
{
    public int Size;
    public int MonitorLeft;
    public int MonitorTop;
    public int MonitorRight;
    public int MonitorBottom;
    public int WorkLeft;
    public int WorkTop;
    public int WorkRight;
    public int WorkBottom;
    public int Flags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
}
```

---

## Private Data Types

* **`OcrSample(string Name, string FileName, string ExpectedText)`**
  * A sealed record used to map OCR sample names to image files and their expected text outputs.