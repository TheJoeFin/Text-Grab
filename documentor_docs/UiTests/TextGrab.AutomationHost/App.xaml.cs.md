# Technical Documentation: `UiTests/TextGrab.AutomationHost/App.xaml.cs`

## Overview

The `App.xaml.cs` file serves as the code-behind for the WPF (Windows Presentation Foundation) application class within the `TextGrab.AutomationHost` project. Its primary purpose is to manage the application startup lifecycle, parse incoming command-line arguments, and launch the primary application window (`MainWindow`) initialized with those arguments.

---

## File Details

* **File Path:** `UiTests/TextGrab.AutomationHost/App.xaml.cs`
* **Namespace:** `TextGrab.AutomationHost`
* **Base Class:** `System.Windows.Application`

---

## Key Components

### 1. `App` Class
```csharp
public partial class App : Application
```
* **Description:** A partial class deriving from `System.Windows.Application`. It defines the entry point and lifecycle behavior for the WPF application process.

---

### 2. `OnStartup` Method
```csharp
protected override void OnStartup(StartupEventArgs e)
```
* **Description:** An overridden lifecycle method inherited from `System.Windows.Application`. It is automatically executed when the application starts.
* **Parameters:**
  * `StartupEventArgs e`: Contains event arguments related to the application startup, specifically command-line arguments passed via `e.Args`.

---

## How It Works / Execution Flow

When the application is launched, the following steps occur sequentially inside the `OnStartup` method:

1. **Base Initialization:**
   ```csharp
   base.OnStartup(e);
   ```
   Calls the underlying `OnStartup` implementation of the `Application` base class to ensure standard WPF startup processes occur.

2. **Command-Line Parsing:**
   ```csharp
   FixtureOptions options = FixtureOptions.Parse(e.Args);
   ```
   Retrieves the command-line arguments string array (`e.Args`) and passes it to `FixtureOptions.Parse()` to construct a `FixtureOptions` configuration object.

3. **Window Initialization and Display:**
   ```csharp
   new MainWindow(options).Show();
   ```
   Instantiates a new `MainWindow` object, passing the parsed `options` into its constructor, and then displays the window by calling `.Show()`.

---

## Summary of Dependencies Referenced in Code

* **`System.Windows`**: Provides WPF application infrastructure classes (`Application`, `StartupEventArgs`).
* **`FixtureOptions`**: A class used to parse command-line arguments via the `Parse` static method.
* **`MainWindow`**: The application's main window class, which accepts a `FixtureOptions` object in its constructor.