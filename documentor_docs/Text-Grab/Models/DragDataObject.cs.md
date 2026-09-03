# Technical Documentation: `DragDataObject.cs`

**File Path:** `Text-Grab/Models/DragDataObject.cs`  
**Namespace:** `Text_Grab.Models`  
**Class:** `DragDataObject`

---

## Overview

The `DragDataObject` static class provides helper functionality for Windows Drag-and-Drop operations using COM Interop and Shell APIs. It allows the application to:
1. Create a native `IDataObject` from a given file path.
2. Customize the drag thumbnail image displayed attached to the mouse cursor during drag operations (`IDragSourceHelper`).
3. Convert a WPF `BitmapSource` image into a GDI+ `System.Drawing.Bitmap`.

---

## Static Fields

### `DataObject`
* **Type:** `Guid`
* **Value:** `"b8c0bd9f-ed24-455c-83e6-d5390c4fe8c4"`
* **Description:** Represents the standard Shell Bind Handler ID (`BHID_DataObject`) used to retrieve an `IDataObject` interface from an `IShellItem`.

---

## Public Methods

### `FromFile(string filePath)`
Creates and returns a native COM `IDataObject` instance representing a file at the specified file path.

* **Parameters:**
  * `filePath` (`string`): The full target file path on disk.
* **Returns:** `System.Runtime.InteropServices.ComTypes.IDataObject` — The native Shell data object for the file.
* **Operation Flow:**
  1. Calls `SHCreateItemFromParsingName` to obtain an `IShellItem` interface for the specified path.
  2. Binds the `IShellItem` to the `DataObject` handler via `BindToHandler` to request its `IDataObject` representation.
  3. Uses `Marshal.ThrowExceptionForHR` to throw an exception if any HRESULT failure occurs.

---

### `SetDragImage(this IDataObject dataObject, IntPtr hBitmap, int width, int height)`
An extension method for `IDataObject` that attaches a custom visual drag image thumbnail to the cursor during a drag-and-drop operation.

* **Parameters:**
  * `dataObject` (`IDataObject`): The target COM data object. Must not be `null`.
  * `hBitmap` (`IntPtr`): A handle to a Win32 GDI bitmap (`HBITMAP`) representing the thumbnail.
  * `width` (`int`): Width of the thumbnail image in pixels.
  * `height` (`int`): Height of the thumbnail image in pixels.
* **Exceptions:**
  * `ArgumentNullException`: Thrown if `dataObject` is `null`.
  * `COMException`: Thrown via `Marshal.ThrowExceptionForHR` if `InitializeFromBitmap` fails.
* **Operation Flow:**
  1. Validates that `dataObject` is not null using `ArgumentNullException.ThrowIfNull`.
  2. Instantiates a COM object of type `DragDropHelper` (`CLSID_DragDropHelper`).
  3. Populates an `ShDragImage` structure with the handle (`HBmpDragImage`) and size dimensions (`SizeDragImage`).
  4. Calls `InitializeFromBitmap` on the `IDragSourceHelper` interface to bind the bitmap preview to the drag operation.

---

### `BitmapSourceToBitmap(MediaImaging.BitmapSource source)`
Converts a WPF image (`System.Windows.Media.Imaging.BitmapSource`) to a GDI+ image (`System.Drawing.Bitmap`).

* **Parameters:**
  * `source` (`System.Windows.Media.Imaging.BitmapSource`): The source WPF bitmap image.
* **Returns:** `System.Drawing.Bitmap?` — A GDI+ bitmap containing the copied pixel data, or `null` if the input `source` is `null`.
* **Operation Flow:**
  1. Checks if `source` is `null`; if so, returns `null`.
  2. Creates a new `System.Drawing.Bitmap` with matching width/height and pixel format `Format32bppArgb`.
  3. Locks the target bitmap's pixel bits in memory using `LockBits` in `WriteOnly` mode.
  4. Copies pixel data directly from `source` using `source.CopyPixels` into the allocated memory pointer (`Scan0`).
  5. Unlocks the target bitmap bits using `UnlockBits`.
  6. Returns the populated `Bitmap`.

---

## Interop Definitions (Private & Internal)

### P/Invoke Function

#### `SHCreateItemFromParsingName`
```csharp
[DllImport("shell32", CharSet = CharSet.Unicode)]
private static extern int SHCreateItemFromParsingName(
    string path, 
    IBindCtx? pbc, 
    [MarshalAs(UnmanagedType.LPStruct)] Guid riid, 
    out IShellItem ppv);
```
Creates a Shell item object from a parsing name (file path).

---

### COM Interfaces and Classes

#### `IShellItem`
* **GUID:** `"43826d1e-e718-42ee-bc55-a1e261c37bfe"`
* **Interface Type:** `ComInterfaceType.InterfaceIsIUnknown`
* **Methods:**
  * `BindToHandler(IBindCtx? pbc, Guid bhid, Guid riid, out object ppv)`: Binds to a specified handler (such as `BHID_DataObject`) for the Shell item.

#### `DragDropHelper`
* **CLSID:** `"4657278a-411b-11d2-839a-00c04fd918d0"`
* CoClass used to instantiate the native Shell Drag Drop Helper.

#### `IDragSourceHelper`
* **GUID:** `"DE5BF786-477A-11D2-839D-00C04FD918D0"`
* **Interface Type:** `ComInterfaceType.InterfaceIsIUnknown`
* **Methods:**
  * `InitializeFromBitmap(ref ShDragImage pShDrawImage, IDataObject pDataObject)`: Sets the image thumbnail for the drag source object.

---

### Internal Structures

#### `ShDragImage`
[Sequential Layout] Represents the native Windows `SHDRAGIMAGE` structure.

| Field | Type | Description |
| :--- | :--- | :--- |
| `SizeDragImage` | `System.Drawing.Size` | Width and height of the drag image. |
| `PtOffset` | `System.Drawing.Point` | Offset of the cursor within the drag image. |
| `HBmpDragImage` | `IntPtr` | Handle to the drag bitmap (`HBITMAP`). |
| `CrColorKey` | `int` | Color key used to mask the background color. |