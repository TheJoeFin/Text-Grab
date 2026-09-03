# Technical Documentation: `UiAutomationOptions.cs`

**File Path:** `Text-Grab/Models/UiAutomationOptions.cs`  
**Namespace:** `Text_Grab.Models`

---

## Overview

The `UiAutomationOptions` record is a data model in the Text-Grab application that encapsulates configuration parameters used during UI Automation operations. Defined as a C# positional `record`, it provides an immutable, value-based data container for controlling how UI elements are queried, traversed, and filtered.

---

## Definition & Syntax

```csharp
using System.Windows;

namespace Text_Grab.Models;

public record UiAutomationOptions(
    UiAutomationTraversalMode TraversalMode,
    bool IncludeOffscreen,
    bool PreferFocusedElement,
    Rect? FilterBounds = null);
```

---

## Key Components

`UiAutomationOptions` consists of four positional parameters that automatically translate into public init-only properties:

### 1. `TraversalMode`
* **Type:** `UiAutomationTraversalMode`
* **Description:** Specifies the strategy or mode to be used when traversing the UI Automation element tree.

### 2. `IncludeOffscreen`
* **Type:** `bool`
* **Description:** A boolean flag that determines whether elements that are currently off-screen should be included during UI Automation processing.

### 3. `PreferFocusedElement`
* **Type:** `bool`
* **Description:** A boolean flag indicating whether the automation logic should prioritize or prefer the element currently holding focus.

### 4. `FilterBounds`
* **Type:** `Rect?` (`System.Windows.Rect` nullable)
* **Default Value:** `null`
* **Description:** An optional bounding box specified as a WPF `Rect`. When provided, it restricts or filters UI automation operations to elements within the specified rectangular area.

---

## How It Works

1. **Record Semantics:** Being defined as a C# `record`, instances of `UiAutomationOptions` are immutable by default and feature built-in value-based equality and formatted string representation (`ToString()`).
2. **Default Parameter Value:** The `FilterBounds` parameter is optional with a default value of `null`. This allows instantiating `UiAutomationOptions` without providing a bounding rectangle if filtering by region is not required.
3. **External Dependencies:** Uses `System.Windows.Rect` from the `System.Windows` namespace for representing bounding coordinates.