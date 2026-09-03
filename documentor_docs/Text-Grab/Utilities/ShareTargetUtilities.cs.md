# Technical Documentation Guide: `ShareTargetUtilities.cs`

## Overview

The `ShareTargetUtilities` static class in the `Text_Grab.Utilities` namespace provides utility methods to detect and handle application activations resulting from Windows Share Contract interactions (Share Target). 

When a user shares content (files, images, text, or URIs) from another application into Text-Grab via the system share menu, this class processes the incoming shared data package, formats or saves the data as necessary, and routes it to the appropriate user interface window (`GrabFrame` or `EditTextWindow`).

---

## Class Architecture

- **Namespace**: `Text_Grab.Utilities`
- **Class Type**: `public static class`
- **Dependencies**: 
  - `Microsoft.Windows.AppLifecycle`
  - `Windows.ApplicationModel.Activation`
  - `Windows.ApplicationModel.DataTransfer`
  - `Windows.ApplicationModel.DataTransfer.ShareTarget`
  - `Windows.Storage`
  - `Text_Grab.Views` (`GrabFrame`, `EditTextWindow`)

---

## Public Methods

### `IsShareTargetActivation()`

Determines if the current application instance was launched or activated specifically as a Share Target.

* **Signature**:
  ```csharp
  public static bool IsShareTargetActivation()
  ```
* **Returns**: `bool` — `true` if the activation kind is `ExtendedActivationKind.ShareTarget`; otherwise, `false`.
* **Behavior**:
  1. Retrieves current activation arguments using `AppInstance.GetCurrent().GetActivatedEventArgs()`.
  2. Compares `args.Kind` with `ExtendedActivationKind.ShareTarget`.
  3. Catches any exception during retrieval, logs the error using `Debug.WriteLine`, and returns `false`.

---

### `HandleShareTargetActivationAsync()`

Processes the incoming shared data package when activated as a Share Target.

* **Signature**:
  ```csharp
  public static async Task<bool> HandleShareTargetActivationAsync()
  ```
* **Returns**: `Task<bool>` — `true` if shared content was successfully recognized and routed to a window; `false` if the activation is invalid, unsupported, or fails.
* **Behavior**:
  1. Validates that the current activation kind is `ExtendedActivationKind.ShareTarget`.
  2. Casts `args.Data` to `ShareTargetActivatedEventArgs`.
  3. Extracts `ShareOperation` and its associated `DataPackageView`.
  4. Evaluates supported data formats in the following precedence order:
     1. **Storage Items** (`StandardDataFormats.StorageItems`): Calls `HandleSharedStorageItemsAsync`.
     2. **Bitmap** (`StandardDataFormats.Bitmap`): Calls `HandleSharedBitmapAsync`.
     3. **Text** (`StandardDataFormats.Text`): Calls `HandleSharedTextAsync`.
     4. **URI** (`StandardDataFormats.Uri`): Calls `HandleSharedUriAsync`.
  5. Invokes `shareOperation.ReportCompleted()` to notify the system that the share action has concluded.
  6. Catches exceptions, logs them with `Debug.WriteLine`, and returns `false`.

---

## Private Handler Methods

### `HandleSharedStorageItemsAsync(DataPackageView data)`

Processes shared file system items (`IStorageItem`).

* **Signature**:
  ```csharp
  private static async Task<bool> HandleSharedStorageItemsAsync(DataPackageView data)
  ```
* **Processing Logic**:
  1. Asynchronously retrieves storage items via `data.GetStorageItemsAsync()`.
  2. **Image Check Loop**: Iterates through items. If an item is a `StorageFile` and its extension passes `IoUtilities.IsImageFileExtension()`:
     - Instantiates a `GrabFrame` window with the file path (`file.Path`).
     - Calls `Show()` and `Activate()` on the `GrabFrame`.
     - Returns `true`.
  3. **Text Check Loop**: If no image files were handled, iterates through items again trying to read non-image `StorageFile` contents:
     - Reads text using `FileIO.ReadTextAsync(file)`.
     - Instantiates an `EditTextWindow`.
     - Appends the content via `etw.AddThisText(text)`.
     - Displays and activates the window (`Show()`, `Activate()`).
     - Returns `true`.
     - Catches and logs exceptions per file if reading fails, allowing remaining items to be evaluated.
  4. Returns `false` if no items were handled.

---

### `HandleSharedBitmapAsync(DataPackageView data)`

Handles in-memory or stream-based bitmap data shared into the application.

* **Signature**:
  ```csharp
  private static async Task<bool> HandleSharedBitmapAsync(DataPackageView data)
  ```
* **Processing Logic**:
  1. Asynchronously obtains a `RandomAccessStreamReference` from `data.GetBitmapAsync()`.
  2. Opens the stream for reading.
  3. Generates a temporary PNG file path inside `AutomationProfile.GetTemporaryDirectory()` formatted as `TextGrab_Share_{Guid}.png`.
  4. Asynchronously reads the raw bytes from the stream using a `DataReader` and writes them to disk via `File.Create`.
  5. Instantiates `GrabFrame` with the path to the newly created temporary PNG file.
  6. Calls `Show()` and `Activate()` on the `GrabFrame`.
  7. Returns `true`.

---

### `HandleSharedTextAsync(DataPackageView data)`

Handles plain text shared from another application.

* **Signature**:
  ```csharp
  private static async Task<bool> HandleSharedTextAsync(DataPackageView data)
  ```
* **Processing Logic**:
  1. Asynchronously fetches the text string via `data.GetTextAsync()`.
  2. Instantiates an `EditTextWindow`.
  3. Passes the string to the window using `etw.AddThisText(text)`.
  4. Calls `Show()` and `Activate()` on the `EditTextWindow`.
  5. Returns `true`.

---

### `HandleSharedUriAsync(DataPackageView data)`

Handles shared URIs/URLs.

* **Signature**:
  ```csharp
  private static async Task<bool> HandleSharedUriAsync(DataPackageView data)
  ```
* **Processing Logic**:
  1. Asynchronously retrieves the `Uri` object via `data.GetUriAsync()`.
  2. Instantiates an `EditTextWindow`.
  3. Passes the URI string representation using `etw.AddThisText(uri.ToString())`.
  4. Calls `Show()` and `Activate()` on the `EditTextWindow`.
  5. Returns `true`.

---

## Data Routing Summary

| Data Format | Target UI View | Output Action |
| :--- | :--- | :--- |
| **Image File** (`StorageItems`) | `GrabFrame` | Opened directly using file path |
| **Text File** (`StorageItems`) | `EditTextWindow` | Content read via `FileIO` and loaded into window |
| **Bitmap** (`Bitmap`) | `GrabFrame` | Stream saved to temporary `.png` file, then opened |
| **Plain Text** (`Text`) | `EditTextWindow` | Text string loaded directly into window |
| **URI** (`Uri`) | `EditTextWindow` | String representation of URI loaded into window |

---

## Error Handling

- **Activation Failures**: Caught within `IsShareTargetActivation()` and `HandleShareTargetActivationAsync()`, returning `false` without crashing the application.
- **File Read Failures**: Caught locally within `HandleSharedStorageItemsAsync()`, logging errors via `Debug.WriteLine` and allowing execution to continue to attempt reading subsequent files.
- **System Notification**: `shareOperation.ReportCompleted()` is called at the end of `HandleShareTargetActivationAsync()` to notify the Windows Share subsystem that the operation finished.