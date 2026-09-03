# Technical Documentation: `FixtureStateWriter.cs`

**File Path:** `UiTests/TextGrab.AutomationHost/FixtureStateWriter.cs`  
**Namespace:** `TextGrab.AutomationHost`

---

## Overview

The `FixtureStateWriter.cs` file provides functionality for logging and persisting UI automation test state information to a specified output file. It converts automation fixture state objects (`FixtureState`) into line-delimited JSON entries using System.Text.Json source generation for fast, reflection-free serialization.

---

## Key Components

The file defines three primary types:

1. **`FixtureStateWriter`**: A `sealed` class responsible for managing the file target and writing `FixtureState` records to disk.
2. **`FixtureState`**: A `sealed record` representing a snapshot of a UI automation test state event.
3. **`FixtureJsonContext`**: An `internal sealed partial` class derived from `JsonSerializerContext` that provides compile-time source generation for JSON serialization of `FixtureState`.

---

## Component Details

### 1. `FixtureState` Record

`FixtureState` is an immutable data transfer object that captures contextual information during an automation test run.

```csharp
public sealed record FixtureState(
    DateTimeOffset TimestampUtc,
    string Event,
    string Surface,
    string DisplayText,
    string ReceivedText,
    string Bounds,
    string Monitor,
    uint Dpi);
```

#### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `TimestampUtc` | `DateTimeOffset` | The UTC timestamp when the state event occurred. |
| `Event` | `string` | The name or type of event being recorded. |
| `Surface` | `string` | The target UI surface associated with the event. |
| `DisplayText` | `string` | The text displayed on the surface or UI component. |
| `ReceivedText` | `string` | The text captured or received during the automation test. |
| `Bounds` | `string` | The bounding box/coordinate boundaries of the element or surface. |
| `Monitor` | `string` | Information identifying the active display/monitor. |
| `Dpi` | `uint` | The DPI (dots per inch) setting of the display environment. |

---

### 2. `FixtureJsonContext` Class

```csharp
[System.Text.Json.Serialization.JsonSerializable(typeof(FixtureState))]
internal sealed partial class FixtureJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
```

* **Inherits from:** `System.Text.Json.Serialization.JsonSerializerContext`
* **Attributes:** `[JsonSerializable(typeof(FixtureState))]`
* **Purpose:** Enables C# source generation for `FixtureState` JSON serialization. This avoids dynamic runtime reflection, improving performance and trimming compatibility.

---

### 3. `FixtureStateWriter` Class

The primary service class that writes `FixtureState` records to the output file.

```csharp
public sealed class FixtureStateWriter(string? stateFile)
```

#### Primary Constructor & Initialization

* Accepts a nullable `string? stateFile` parameter.
* If `stateFile` is `null`, empty, or composed only of whitespace, the private `stateFile` field is assigned `null`.
* If `stateFile` contains a valid path string, it is resolved to its fully qualified path via `Path.GetFullPath(stateFile)`.

#### `Write` Method

```csharp
public void Write(FixtureState state)
```

**How it works:**

1. **Null Check:** Evaluates whether `stateFile` is `null`. If `null`, the method immediately returns without performing any file operations.
2. **Directory Creation:** Resolves the target directory path using `Path.GetDirectoryName(stateFile)`. If a non-empty directory path exists, `Directory.CreateDirectory(directory)` is called to ensure the target destination directory exists.
3. **Serialization & Append:** Serializes the `FixtureState` instance using `JsonSerializer.Serialize` with `FixtureJsonContext.Default.FixtureState`.
4. **File Output:** Appends the serialized JSON string followed by `Environment.NewLine` to the file via `File.AppendAllText`. Each state record is stored on its own line (JSON Lines format).

---

## Workflow Execution Summary

```
[ Call FixtureStateWriter.Write(state) ]
                   │
                   ▼
         Is stateFile null?
        ├── Yes ──► [ Return / Do Nothing ]
        │
        └── No
             │
             ▼
   [ Get Directory Path ]
             │
             ▼
 Does directory exist / need creation?
             │
             ▼
   [ Directory.CreateDirectory ]
             │
             ▼
[ Serialize state using FixtureJsonContext ]
             │
             ▼
[ File.AppendAllText (JSON + Environment.NewLine) ]
```