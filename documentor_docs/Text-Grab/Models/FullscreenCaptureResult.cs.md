# Technical Documentation: `FullscreenCaptureResult.cs`

**File Path:** `Text-Grab/Models/FullscreenCaptureResult.cs`  
**Namespace:** `Text_Grab.Models`  
**Type:** `record`

---

## Overview

The `FullscreenCaptureResult` record is a data model used within Text-Grab to encapsulate the outcome of a fullscreen capture operation. It stores details such as the selection style, capture area dimensions, optional captured image data, and target window title. Additionally, it provides computed properties that evaluate capability flags based on the capture state.

---

## Dependencies

* `System.Windows`: Provides the `Rect` struct representing rectangular dimensions.
* `System.Windows.Media.Imaging`: Provides the `BitmapSource` class for handling image data.

---

## Type Definition

```csharp
public record FullscreenCaptureResult(
    FsgSelectionStyle SelectionStyle,
    Rect CaptureRegion,
    BitmapSource? CapturedImage = null,
    string? WindowTitle = null)
```

As an immutable C# positional record, `FullscreenCaptureResult` provides built-in value-based equality semantics and standard object methods (`ToString`, `GetHashCode`, etc.).

---

## Record Parameters / Positional Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `SelectionStyle` | `FsgSelectionStyle` | *None (Required)* | Specifies the selection style used during the capture operation. |
| `CaptureRegion` | `Rect` | *None (Required)* | The rectangular boundaries (`System.Windows.Rect`) of the captured screen area. |
| `CapturedImage` | `BitmapSource?` | `null` | An optional image source containing the visual pixels of the captured region. |
| `WindowTitle` | `string?` | `null` | An optional string containing the title of the window involved in the capture. |

---

## Computed Properties

`FullscreenCaptureResult` defines three read-only computed properties that infer capability states based on the record's property values:

### 1. `SupportsTemplateActions`
```csharp
public bool SupportsTemplateActions => SelectionStyle != FsgSelectionStyle.Freeform;
```
* **Type:** `bool`
* **Description:** Indicates whether template actions are supported. Evaluates to `true` as long as `SelectionStyle` is **not** set to `FsgSelectionStyle.Freeform`.

### 2. `SupportsPreviousRegionReplay`
```csharp
public bool SupportsPreviousRegionReplay =>
    SelectionStyle is FsgSelectionStyle.Region or FsgSelectionStyle.AdjustAfter;
```
* **Type:** `bool`
* **Description:** Indicates whether the previous capture region can be replayed. Evaluates to `true` if `SelectionStyle` is either `FsgSelectionStyle.Region` or `FsgSelectionStyle.AdjustAfter`.

### 3. `UsesCapturedImage`
```csharp
public bool UsesCapturedImage => CapturedImage is not null;
```
* **Type:** `bool`
* **Description:** Indicates whether an image artifact was included with the result. Evaluates to `true` if `CapturedImage` contains a non-null `BitmapSource`.

---

## How It Works

1. **Instantiation:** A caller creates an instance of `FullscreenCaptureResult` by passing the required `SelectionStyle` and `CaptureRegion`, optionally supplying a `CapturedImage` bitmap or a `WindowTitle` string.
2. **Immutability:** The data held by the record cannot be modified post-instantiation.
3. **Capability Checks:** External consumers of this record can query the boolean properties (`SupportsTemplateActions`, `SupportsPreviousRegionReplay`, `UsesCapturedImage`) to determine what downstream operations or feature workflows are supported by this specific capture result.