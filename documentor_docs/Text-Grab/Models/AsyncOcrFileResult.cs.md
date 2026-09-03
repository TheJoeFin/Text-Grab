# Technical Documentation: `AsyncOcrFileResult.cs`

**File Path:** `Text-Grab/Models/AsyncOcrFileResult.cs`  
**Namespace:** `Text_Grab.Models`

---

## Overview

The `AsyncOcrFileResult` record is a lightweight data model used within the `Text_Grab.Models` namespace. It encapsulates the pairing of a local file path with its corresponding OCR (Optical Character Recognition) text result.

---

## Key Components

### 1. Record Declaration
```csharp
public record AsyncOcrFileResult
```
* **Type:** `record`
* **Accessibility:** `public`
* Declared as a C# record type, providing value-based equality semantics out of the box for instances of this model.

---

### 2. Properties

#### `FilePath`
```csharp
public string FilePath { get; init; }
```
* **Type:** `string`
* **Access Modifiers:** `get; init;`
* **Description:** Represents the system or relative file path of the image/file being processed.
* **Immutability:** Uses the `init` accessor, meaning it can only be set during object instantiation (either via the constructor or an object initializer) and cannot be modified thereafter.

#### `OcrResult`
```csharp
public string? OcrResult { get; set; }
```
* **Type:** `string?` (Nullable String)
* **Access Modifiers:** `get; set;`
* **Description:** Holds the text output extracted from the OCR process.
* **Mutability:** Can be set or updated at any point after the object has been instantiated. It is nullable (`string?`), meaning it defaults to `null` before OCR processing completes or if no text is found.

---

### 3. Constructor

```csharp
public AsyncOcrFileResult(string filePath)
{
    FilePath = filePath;
}
```
* **Parameters:** `string filePath`
* **Behavior:** Initializes a new instance of `AsyncOcrFileResult` by setting the `FilePath` property to the provided parameter value. The `OcrResult` property remains `null` by default until explicitly assigned.

---

## How It Works

1. **Instantiation:** An instance is created by supplying the file path to the constructor:
   ```csharp
   var result = new AsyncOcrFileResult("C:\\Images\\sample.png");
   ```
   At this point, `result.FilePath` is set to `"C:\\Images\\sample.png"`, and `result.OcrResult` is `null`.

2. **Result Assignment:** Once the asynchronous OCR operation for the file completes, the extracted string can be assigned directly to the `OcrResult` property:
   ```csharp
   result.OcrResult = "Extracted text content...";
   ```