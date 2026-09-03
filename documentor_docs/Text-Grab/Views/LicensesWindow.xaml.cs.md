# Technical Documentation: `LicensesWindow.xaml.cs`

## Overview

The `LicensesWindow.xaml.cs` file is the code-behind implementation for the `LicensesWindow` view within the `Text_Grab.Views` namespace. It defines a user interface window inheriting from `Wpf.Ui.Controls.FluentWindow` that displays third-party software packages and license information, providing interaction handlers to view license notices, project URLs, and related system directories.

---

## File Details

* **File Path:** `Text-Grab/Views/LicensesWindow.xaml.cs`
* **Namespace:** `Text_Grab.Views`
* **Base Class:** `Wpf.Ui.Controls.FluentWindow`

---

## Class Architecture & Properties

### Properties

#### `Packages`
```csharp
public ObservableCollection<ThirdPartyPackageInfo> Packages { get; }
```
* **Type:** `ObservableCollection<ThirdPartyPackageInfo>`
* **Access:** Read-only (`get;`)
* **Description:** Holds a collection of `ThirdPartyPackageInfo` objects. It is initialized directly from `ThirdPartyNoticeUtilities.Packages` using spread syntax (`[.. ThirdPartyNoticeUtilities.Packages]`). This property is exposed for Data Binding in the XAML view.

---

## Constructor

### `LicensesWindow()`
```csharp
public LicensesWindow()
```
Initializes a new instance of the `LicensesWindow` class.
* **Execution Flow:**
  1. `InitializeComponent()`: Loads and initializes the associated XAML UI components.
  2. `App.SetTheme()`: Invokes the application-level method to apply the current visual theme.
  3. `DataContext = this;`: Sets the window's data context to itself, enabling visual components to bind directly to properties such as `Packages`.

---

## Event Handlers

### `BuiltWithButton_Click`
```csharp
private void BuiltWithButton_Click(object sender, RoutedEventArgs e)
```
* **Parameters:**
  * `sender`: The object that triggered the click event.
  * `e`: Event arguments (`RoutedEventArgs`).
* **Behavior:** Invokes `ThirdPartyNoticeUtilities.OpenBuiltWithFile()` to open the file detailing libraries or components used to build the application.

---

### `NoticesFolderButton_Click`
```csharp
private void NoticesFolderButton_Click(object sender, RoutedEventArgs e)
```
* **Parameters:**
  * `sender`: The object that triggered the click event.
  * `e`: Event arguments (`RoutedEventArgs`).
* **Behavior:** Invokes `ThirdPartyNoticeUtilities.OpenNoticesDirectory()` to open the local system directory containing third-party notice files.

---

### `NoticeButton_Click`
```csharp
private void NoticeButton_Click(object sender, RoutedEventArgs e)
```
* **Parameters:**
  * `sender`: The UI element triggering the click event.
  * `e`: Event arguments (`RoutedEventArgs`).
* **Behavior:**
  * Uses C# pattern matching (`if (sender is FrameworkElement { DataContext: ThirdPartyPackageInfo package })`) to check if the `sender` is a `FrameworkElement` with a `DataContext` of type `ThirdPartyPackageInfo`.
  * If the condition is met, calls `ThirdPartyNoticeUtilities.OpenNoticeFile(package)` using the extracted `package` instance to open the specific package's notice file.

---

### `ProjectButton_Click`
```csharp
private void ProjectButton_Click(object sender, RoutedEventArgs e)
```
* **Parameters:**
  * `sender`: The UI element triggering the click event.
  * `e`: Event arguments (`RoutedEventArgs`).
* **Behavior:**
  * Uses C# pattern matching (`if (sender is FrameworkElement { DataContext: ThirdPartyPackageInfo package })`) to inspect the `sender` element and extract its associated `ThirdPartyPackageInfo` model.
  * If successfully extracted, calls `ThirdPartyNoticeUtilities.OpenProjectUrl(package)` to open the project's external URL.

---

## Dependencies & External Utilities

The class interacts with the following internal models and utilities:

* **`Text_Grab.Models.ThirdPartyPackageInfo`**: The data model representing individual package information.
* **`Text_Grab.Utilities.ThirdPartyNoticeUtilities`**: Static utility class providing methods to retrieve packages (`Packages`) and handle file/URL actions (`OpenBuiltWithFile`, `OpenNoticesDirectory`, `OpenNoticeFile`, `OpenProjectUrl`).
* **`Text_Grab.App`**: Provides the static `SetTheme()` method for managing visual themes.
* **`Wpf.Ui.Controls.FluentWindow`**: Base class providing Fluent UI window styling and capabilities.