# Technical Documentation: `RegExIcon.xaml.cs`

## Overview

The `RegExIcon.xaml.cs` file contains the code-behind class for the `RegExIcon` user control within the Text-Grab application. It defines a custom WPF (Windows Presentation Foundation) `UserControl` responsible for rendering a Regular Expression icon UI component.

## File Information

* **File Path:** `Text-Grab/Controls/RegExIcon.xaml.cs`
* **Namespace:** `Text_Grab.Controls`
* **Class Name:** `RegExIcon`
* **Base Class:** `System.Windows.Controls.UserControl`

---

## Code Breakdown

### Dependencies

```csharp
using System.Windows.Controls;
```
Imports the standard WPF controls namespace, providing access to the `UserControl` base class.

---

### Class Declaration

```csharp
namespace Text_Grab.Controls;

public partial class RegExIcon : UserControl
```

* **Namespace:** Belongs to `Text_Grab.Controls`.
* **Access Modifier:** `public` — accessible throughout the application.
* **Class Type:** `partial` — works in conjunction with the auto-generated partial class from the corresponding XAML file (`RegExIcon.xaml`).
* **Inheritance:** Inherits from `System.Windows.Controls.UserControl`.

---

### Constructor

```csharp
public RegExIcon()
{
    DataContext = this;
    InitializeComponent();
}
```

The default public constructor performs the following initialization steps:

1. **`DataContext = this;`**  
   Sets the `DataContext` of the control to its own instance. This allows elements defined in the corresponding XAML file to bind directly to properties or context provided by this class.

2. **`InitializeComponent();`**  
   Executes the auto-generated WPF method that loads and parses the XAML file associated with this control, connecting declared UI elements and event handlers.

---

## How It Works

1. When an instance of `RegExIcon` is created, its constructor executes.
2. The control assigns itself as its own `DataContext`.
3. `InitializeComponent()` is called to draw and render the visual layout defined in `RegExIcon.xaml`.