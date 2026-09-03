# Technical Documentation: `StorageFileExtensions.cs`

**File Path:** `Text-Grab/Extensions/StorageFileExtensions.cs`  
**Namespace:** `Text_Grab.Extensions`  
**Access Modifier:** `internal static`

---

## 1. Overview

The `StorageFileExtensions` class provides extension methods on `string` instances (representing file paths) to read text asynchronously or convert paths into an `IRandomAccessStream`.

Its primary purpose is to abstract file access logic depending on whether the application is running as a **Packaged App** (MSIX / UWP context) or an **Unpackaged App** (standard Win32/desktop execution context).

---

## 2. Dependencies & Imports

* `System`: Core system types and base runtime support (`Environment`, `Uri`, `AppContext`).
* `System.IO`: Standard file I/O operations (`File`, `Path`, `MemoryStream`).
* `System.Threading.Tasks`: Asynchronous programming support (`Task`).
* `Windows.Storage`: Universal Windows Platform (UWP) storage APIs (`StorageFile`, `FileIO`).
* `Windows.Storage.Streams`: Stream operations for WinRT/UWP interfaces (`IRandomAccessStream`).

---

## 3. Fields

### `IsPackagedApp`
* **Type:** `private static bool`
* **Purpose:** Determines whether the application is currently running as a packaged application.
* **Logic:** Evaluates `Environment.GetEnvironmentVariable("PACKAGED_PRODUCT_ID") != null`. If the environment variable `PACKAGED_PRODUCT_ID` is present, it returns `true`; otherwise, `false`.

---

## 4. Public / Internal Extension Methods

### `ReadTextAsync(this string filepath)`
Asynchronously reads and returns the full text content of a file located at the specified relative file path.

* **Signature:**
  ```csharp
  internal static async Task<string> ReadTextAsync(this string filepath)
  ```
* **Parameters:**
  * `filepath` (`string`): The relative path to the file.
* **Return Value:** `Task<string>` — The text content of the file, or `string.Empty` if the input path is null or whitespace.
* **Execution Steps:**
  1. Validates input using `string.IsNullOrWhiteSpace(filepath)`. Returns `string.Empty` if true.
  2. **If packaged (`IsPackagedApp == true`):**
     * Obtains a `StorageFile` by calling `CreateStorageFile(filepath)`.
     * Reads text asynchronously via `FileIO.ReadTextAsync(file)`.
     * Returns the content string.
  3. **If unpackaged (`IsPackagedApp == false`):**
     * Combines the `filepath` with the application's base directory via `CombineWithBasePath(filepath)`.
     * Reads text asynchronously using `File.ReadAllTextAsync(filePath)`.
     * Returns the content string.

---

### `CreateStreamAsync(this string filepath)`
Asynchronously opens a read-only stream for the specified relative file path, returned as an `IRandomAccessStream`.

* **Signature:**
  ```csharp
  internal static async Task<IRandomAccessStream> CreateStreamAsync(this string filepath)
  ```
* **Parameters:**
  * `filepath` (`string`): The relative path to the file.
* **Return Value:** `Task<IRandomAccessStream>` — A stream opened for read access, or a null memory stream (`MemoryStream.Null.AsRandomAccessStream()`) if the input path is null or whitespace.
* **Execution Steps:**
  1. Validates input using `string.IsNullOrWhiteSpace(filepath)`. Returns `MemoryStream.Null.AsRandomAccessStream()` if true.
  2. **If packaged (`IsPackagedApp == true`):**
     * Obtains a `StorageFile` by calling `CreateStorageFile(filepath)`.
     * Opens the file stream for reading via `file.OpenAsync(FileAccessMode.Read)`.
     * Returns the resulting `IRandomAccessStream`.
  3. **If unpackaged (`IsPackagedApp == false`):**
     * Combines `filepath` with the application's base directory via `CombineWithBasePath(filepath)`.
     * Opens the file with `File.OpenRead(filePath)`.
     * Converts the standard `FileStream` to an `IRandomAccessStream` using `.AsRandomAccessStream()`.

---

## 5. Private Helper Methods

### `CreateStorageFile(string filepath)`
Constructs a UWP `StorageFile` reference from a relative path using the application package URI scheme (`ms-appx:///`).

* **Signature:**
  ```csharp
  private static Task<StorageFile> CreateStorageFile(string filepath)
  ```
* **Parameters:**
  * `filepath` (`string`): The relative path inside the package.
* **Return Value:** `Task<StorageFile>`
* **Implementation Detail:**
  ```csharp
  Uri uri = new Uri("ms-appx:///" + filepath);
  return StorageFile.GetFileFromApplicationUriAsync(uri).AsTask();
  ```

---

### `CombineWithBasePath(string filepath)`
Combines a relative path string with the application runtime base directory.

* **Signature:**
  ```csharp
  private static string CombineWithBasePath(string filepath)
  ```
* **Parameters:**
  * `filepath` (`string`): The relative path.
* **Return Value:** `string` — Full absolute path targeting `AppContext.BaseDirectory`.
* **Implementation Detail:**
  ```csharp
  return Path.Combine(AppContext.BaseDirectory, filepath);
  ```

---

## 6. Execution Flow Summary

```
                       [ Input filepath ]
                               |
                   Is string null/whitespace?
                     /                   \
                   YES                   NO
                   /                       \
   [ Return Empty Result ]           IsPackagedApp?
   - ReadTextAsync -> ""                /        \
   - CreateStreamAsync -> NullStream  YES         NO
                                      /             \
                  Use Uri "ms-appx:///"     Combine with AppContext.BaseDirectory
                           |                                   |
                  Get StorageFile via                 Perform System.IO File
                     StorageFile API                        Operations
```