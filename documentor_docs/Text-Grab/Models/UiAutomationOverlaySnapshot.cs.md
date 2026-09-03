# Technical Documentation: `UiAutomationOverlaySnapshot.cs`

**File Path:** `Text-Grab/Models/UiAutomationOverlaySnapshot.cs`  
**Namespace:** `Text_Grab.Models`

---

## 1. Overview

The `UiAutomationOverlaySnapshot` record is a data model within the `Text_Grab.Models` namespace. It encapsulates a snapshot of UI automation overlay data captured at a specific moment. It stores the bounding area of the capture, the targeted window, and a read-only list of UI automation overlay items located within that region.

---

## 2. Type Definition

```csharp
public record UiAutomationOverlaySnapshot(
    Rect CaptureBounds,
    WindowSelectionCandidate TargetWindow,
    IReadOnlyList<UiAutomationOverlayItem> Items)
```

`UiAutomationOverlaySnapshot` is declared as a positional **`record`**, providing standard C# record capabilities (value-based equality, immutability, and concise syntax).

### Dependencies
* `System.Collections.Generic` — Provides `IReadOnlyList<T>`.
* `System.Windows` — Provides the `Rect` struct.

---

## 3. Properties

### Positional Constructor Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `CaptureBounds` | `System.Windows.Rect` | Represents the rectangular region defining the boundary of the capture area. |
| `TargetWindow` | `WindowSelectionCandidate` | Specifies the target window object associated with the snapshot. |
| `Items` | `IReadOnlyList<UiAutomationOverlayItem>` | A read-only list containing the `UiAutomationOverlayItem` elements present in the snapshot. |

### Calculated Properties

#### `HasItems`
```csharp
public bool HasItems => Items.Count > 0;
```
* **Type:** `bool`
* **Access:** Read-only (`get`)
* **Behavior:** Evaluates whether the `Items` collection contains at least one element (`Items.Count > 0`). Returns `true` if elements are present; otherwise, `false`.

---

## 4. Key Functionality & Behavior

1. **Immutability:** As a C# record with positional parameters, instances of `UiAutomationOverlaySnapshot` are immutable by default upon initialization.
2. **Item Check Convenience:** The `HasItems` computed property provides a quick boolean check to verify if any overlay items exist in the `Items` collection without requiring callers to inspect `Items.Count` directly.