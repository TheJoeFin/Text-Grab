# Technical Documentation: `OcrDirectoryOptions.cs`

**File Location:** `Text-Grab/Models/OcrDirectoryOptions.cs`  
**Namespace:** `Text_Grab.Models`

---

## Overview

The `OcrDirectoryOptions` file defines a C# `record` that serves as a configuration data model for directory-level Optical Character Recognition (OCR) operations in Text-Grab. It encapsulates user settings and flags governing how a directory of images or files should be scanned, formatted, and output.

---

## Type Definition

```csharp
public record OcrDirectoryOptions
```

Because it is defined as a C# `record`, `OcrDirectoryOptions` provides built-in value-based equality semantics and concise syntax while maintaining mutable auto-properties with `{ get; set; }`.

---

## Properties & Defaults

The record contains seven public properties that store configuration parameters and flags for directory processing:

| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Path` | `string` | `string.Empty` | The file system path of the target directory to perform OCR on. |
| `IsRecursive` | `bool` | `false` | Indicates whether the OCR operation should recursively process subdirectories within the target path. |
| `WriteTxtFiles` | `bool` | `false` | Indicates whether the resulting OCR text should be written out as individual `.txt` files. |
| `OutputFileNames` | `bool` | `false` | Controls whether file names are included in the formatted OCR output. Defaults to `true`. |
| `OutputFooter` | `bool` | `true` | Controls whether a footer section is included in the output. |
| `OutputHeader` | `bool` | `true` | Controls whether a header section is included in the output. |
| `GrabTemplate` | `GrabTemplate?` | `null` | A nullable reference to a `GrabTemplate` instance, used if a specific formatting or extraction template is applied. |

---

## Component Details

1. **`Path`**
   - **Type:** `string`
   - **Default:** `""` (`string.Empty`)
   - Holds the primary path string pointing to the folder containing files to be processed.

2. **`IsRecursive`**
   - **Type:** `bool`
   - **Default:** `false`
   - Determines if the file search strategy includes child directories under `Path`.

3. **`WriteTxtFiles`**
   - **Type:** `bool`
   - **Default:** `false`
   - A toggle controlling whether output text is saved directly to disk as text files.

4. **`OutputFileNames`**
   - **Type:** `bool`
   - **Default:** `true`
   - Specifies if output text segments should be labeled with the corresponding source file names.

5. **`OutputFooter`**
   - **Type:** `bool`
   - **Default:** `true`
   - Specifies whether to attach a footer to the final output text block.

6. **`OutputHeader`**
   - **Type:** `bool`
   - **Default:** `true`
   - Specifies whether to prepending a header to the final output text block.

7. **`GrabTemplate`**
   - **Type:** `GrabTemplate?` (Nullable)
   - **Default:** `null`
   - Holds an optional template object (`GrabTemplate`) to customize how OCR results are parsed or structured.

---

## Functionality & Usage

`OcrDirectoryOptions` strictly acts as a data container. It contains no executable logic, methods, or algorithms itself. Instances of this record are constructed and populated (typically via user interface controls or service callers) and passed into OCR processing services that consume these options to control file iteration, output formatting, and output destination logic.