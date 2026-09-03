# Technical Documentation: `FixtureOptions.cs`

**File Path:** `UiTests/TextGrab.AutomationHost/FixtureOptions.cs`  
**Namespace:** `TextGrab.AutomationHost`

---

## Overview

The `FixtureOptions` record defines the configuration options for a test fixture host in the `TextGrab.AutomationHost` namespace. It provides command-line argument parsing capability to extract configuration parameters (`--surface`, `--state-file`, and `--text`) into a strongly typed, immutable data structure.

---

## Class Definition

```csharp
public sealed record FixtureOptions(string Surface, string? StateFile, string? DisplayText)
```

`FixtureOptions` is defined as a `sealed record` with three positional properties:

| Property | Type | Description |
| :--- | :--- | :--- |
| `Surface` | `string` | Represents the target surface identifier. Defaults to `"KnownText"` if not supplied via command-line arguments. |
| `StateFile` | `string?` | Path to a state file. Defaults to the value of the `TEXT_GRAB_AUTOMATION_HOST_STATE_FILE` environment variable if not specified in arguments. |
| `DisplayText` | `string?` | Optional text to display. Defaults to `null` if not specified. |

---

## Methods

### `Parse`

```csharp
public static FixtureOptions Parse(IEnumerable<string> arguments)
```

Parses an `IEnumerable<string>` collection of command-line arguments and returns a populated `FixtureOptions` instance.

#### Parameters
* **`arguments`**: An `IEnumerable<string>` containing the command-line argument strings to parse.

#### Returns
* **`FixtureOptions`**: A new instance of `FixtureOptions` containing the parsed or default values.

#### Defaults Assigned Before Parsing
* **`surface`**: `"KnownText"`
* **`stateFile`**: Evaluated from `Environment.GetEnvironmentVariable("TEXT_GRAB_AUTOMATION_HOST_STATE_FILE")`
* **`displayText`**: `null`

#### Logic & Parsing Rules
The method converts `arguments` into an array and iterates sequentially over each element:

1. **Inline Assignment (`--option=value`)**:
   It checks if the current argument starts with `--surface=`, `--state-file=`, or `--text=` using the private helper `TryGetValue`.
   * If matching `--surface=`, `surface` is updated to the value after `=`.
   * If matching `--state-file=`, `stateFile` is updated to the value after `=`.
   * If matching `--text=`, `displayText` is updated to the value after `=`.

2. **Space-Separated Assignment (`--option value`)**:
   If the argument is exact-matched against `--surface`, `--state-file`, or `--text`:
   * It checks if a subsequent argument exists in the array (`index + 1 < values.Length`).
   * If a subsequent argument exists, it increments the index pointer (`++index`) and assigns that next argument as the value for the corresponding option.

---

### `TryGetValue`

```csharp
private static bool TryGetValue(string argument, string option, out string value)
```

A private helper method that attempts to extract an option's value from an inline key-value pair formatted as `--option=value`.

#### Parameters
* **`argument`**: The string argument being evaluated (e.g., `--surface=CustomSurface`).
* **`option`**: The target option flag (e.g., `--surface`).
* **`value`**: An `out` parameter that receives the extracted value if matched, or `string.Empty` if not matched.

#### Returns
* **`bool`**: `true` if `argument` starts with `${option}=` (case-insensitive check via `StringComparison.OrdinalIgnoreCase`); otherwise, `false`.

#### Logic
1. Appends `=` to `option` to form the prefix string (e.g., `--surface=`).
2. Checks if `argument` begins with the prefix string using `StringComparison.OrdinalIgnoreCase`.
3. If true, extracts the slice of the string following the prefix using range indexing (`argument[prefix.Length..]`) and sets `value`.
4. If false, sets `value` to `string.Empty` and returns `false`.

---

## Supported Argument Formats

The parser supports two formats for command-line arguments:

1. **Equal-sign delimited**:
   * `--surface=MySurface`
   * `--state-file=C:\path\to\state.json`
   * `--text="Sample text"`

2. **Space-delimited**:
   * `--surface MySurface`
   * `--state-file C:\path\to\state.json`
   * `--text "Sample text"`

*Note: For equal-sign delimited options (`TryGetValue`), key matching is case-insensitive. For exact flag matches (`argument is "--surface" or "--state-file" or "--text"`), exact casing is required.*