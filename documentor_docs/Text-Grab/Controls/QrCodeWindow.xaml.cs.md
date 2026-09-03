# Technical Documentation: `QrCodeWindow.xaml.cs`

## Overview

The `QrCodeWindow` class is a WPF code-behind controller for a Fluent UI window (`FluentWindow`) in the **Text-Grab** application. Its primary responsibility is generating, displaying, and exporting QR codes based on user-provided text. 

Key features implemented in this class include:
- Dynamic QR code generation with text input debouncing.
- Configurable error correction levels (L, M, Q, H).
- Drag-and-drop file support for generated QR code images.
- Copying QR code bitmaps directly to the clipboard.
- Exporting QR codes as PNG or SVG files.
- Resource cleanup on window closure.

---

## Class Definition

- **Namespace**: `Text-Grab.Controls`
- **Base Class**: `Wpf.Ui.Controls.FluentWindow`
- **Class Access**: `public partial class QrCodeWindow`

---

## Fields & Properties

### Private Fields

| Name | Type | Description |
| :--- | :--- | :--- |
| `hBitmap` | `IntPtr` | Handle to a GDI bitmap object created for drag-and-drop visual previews. |
| `qrCodeFileName` | `string` | Base filename generated for the current QR code based on its text content. |
| `tempPath` | `string` | Full path to the temporary PNG file created for drag-and-drop operations. |
| `textDebounceTimer` | `DispatcherTimer` | Timer used to debounce text updates in the input text box to avoid generating QR codes on every keystroke. |
| `errorCorrectionLevel` | `ErrorCorrectionLevel` | Holds the selected ZXing error correction level (Defaults to `ErrorCorrectionLevel.L`). |

### Public Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `QrBitmap` | `Bitmap?` | Gets or sets the GDI+ `Bitmap` instance of the currently generated QR code. |
| `TextOfCode` | `string` | Gets or sets the string content encoded within the QR code. |

---

## Constructor

```csharp
public QrCodeWindow(string textOfCode)
```

### Initialization Logic:
1. Calls `InitializeComponent()` to initialize WPF visual components.
2. Formats the input `textOfCode` into a single line using `.MakeStringSingleLine()`.
3. Assigns the formatted text to `QrCodeTextBox.Text`.
4. Configures `textDebounceTimer` with a **200-millisecond** interval and attaches `TextDebounceTimer_Tick` to its `Tick` event.
5. Invokes `SetQrCodeToText(textOfCode)` to generate the initial QR code image.

---

## Methods & Event Handlers

### Core Generation & Processing Logic

#### `SetQrCodeToText(string textOfCode = "")`
Generates the QR code image, updates UI elements, creates temporary disk artifacts, and generates a GDI bitmap handle.

- **Character Limit**: Restricts `TextOfCode` to a maximum length of **2,953 characters**. If exceeded, the text is truncated and `LengthErrorTextBlock.Visibility` is set to `Visibility.Visible`.
- **Image Generation**: Calls `BarcodeUtilities.GetQrCodeForText()` passing `TextOfCode` and `errorCorrectionLevel`.
- **UI Updates**:
  - Assigns `CodeImage.ToolTip` to the text content.
  - Converts `QrBitmap` to an image source for `CodeImage.Source` using `ImageMethods.BitmapToImageSource()`.
  - Sets window title via `UiTitleBar.Title` (truncating text to 30 characters using `.Truncate(30)`).
- **Temp File & Native Handle**:
  - Truncates filename base to a maximum of 50 characters and removes reserved characters via `ReplaceReservedCharacters()`.
  - Saves a temporary PNG file in `AutomationProfile.GetTemporaryDirectory()`.
  - Obtains `hBitmap` from `QrBitmap.GetHbitmap()` for drag-and-drop preview operations.

#### `TextDebounceTimer_Tick(object? sender, EventArgs e)`
- Handler for `textDebounceTimer.Tick`.
- Calls `SetQrCodeToText(TextOfCode)` when the timer elapses.

---

### User Interaction & UI Events

#### `QrCodeTextBox_TextChanged(object sender, TextChangedEventArgs e)`
- Triggers when the user changes the content of `QrCodeTextBox`.
- Ignores changes if `IsLoaded` is false.
- Resets (stops and restarts) `textDebounceTimer` to delay generation until typing pauses for 200 ms.

#### `ErrorCorrectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)`
- Triggers when the user changes the selection in the error correction combo box.
- Reads the `Tag` property of the selected `ComboBoxItem`.
- Maps tag values (`"L"`, `"M"`, `"Q"`, `"H"`) to `ErrorCorrectionLevel` enum values.
- Re-executes `SetQrCodeToText()` to regenerate the QR code with the updated error correction level.

---

### Export & Clipboard Operations

#### `CodeImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)`
Handles drag-and-drop execution when the user clicks and drags the `CodeImage`.
- **Primary Attempt**: Uses `DragDataObject.FromFile(tempPath)` and attaches `hBitmap` via `.SetDragImage()` to show a visual preview during drag.
- **Fallback**: If `SetDragImage` fails, falls back to a basic `DataObject` using `DataFormats.FileDrop` pointing to `tempPath`.
- Initiates `DragDrop.DoDragDrop()` with `DragDropEffects.Copy`.

#### `CopyButton_Click(object sender, RoutedEventArgs e)`
- Copies the generated `QrBitmap` directly to the system clipboard using `Clipboard.SetData(DataFormats.Bitmap, qrBitmap)`.

#### `SaveButton_Click(object sender, RoutedEventArgs e)`
- Displays a `SaveFileDialog` configured for `.png` files.
- Default directory: `Environment.SpecialFolder.MyPictures`.
- Saves `QrBitmap` as a PNG to the selected file location.

#### `SvgButton_Click(object sender, RoutedEventArgs e)` (Asynchronous)
- Displays a `SaveFileDialog` configured for `.svg` files.
- Generates vector-based SVG output using `BarcodeUtilities.GetSvgQrCodeForText(TextOfCode, errorCorrectionLevel)`.
- Asynchronously saves the SVG text content using `FileUtilities.SaveTextFile()`.

---

### Cleanup & Lifecycle Operations

#### `FluentWindow_Closing(object sender, CancelEventArgs e)`
Executes resource disposal when the window is closed:
1. Releases the unmanaged GDI bitmap handle via `NativeMethods.DeleteObject(hBitmap)`.
2. Deletes the temporary image file stored at `tempPath` if it exists.

---

## Dependencies & External Utilities

- **`ZXing`**: Used for QR code generation internal structures (`ErrorCorrectionLevel`, `SvgRenderer`).
- **`Humanizer`**: Used for string truncation routines (`.Truncate()`).
- **`Text_Grab.Models`**: Application model context (e.g., `AutomationProfile`).
- **`Text_Grab.Utilities`**:
  - `BarcodeUtilities`: Core methods for rendering raster and vector QR codes.
  - `ImageMethods`: Bitmap conversion helpers.
  - `FileUtilities`: Storage and file manipulation functions.
  - `NativeMethods`: P/Invoke declarations (such as `DeleteObject`).
- **`Wpf.Ui.Controls`**: Base UI elements (`FluentWindow`).