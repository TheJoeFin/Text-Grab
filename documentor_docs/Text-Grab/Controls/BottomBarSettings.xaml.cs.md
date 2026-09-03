# Technical Documentation: `BottomBarSettings.xaml.cs`

## Overview

The `BottomBarSettings` class is a WPF code-behind file for the `BottomBarSettings` window (`FluentWindow`) in the **Text-Grab** application. It provides the UI logic for configuring the application's bottom bar layout and toggling individual UI settings. Users can add, remove, reorder, and filter custom bottom bar action buttons, as well as toggle specific display elements (e.g., word count, match count, language picker).

---

## Class Details

* **Namespace:** `Text_Grab.Controls`
* **Class Name:** `BottomBarSettings`
* **Base Class:** `Wpf.Ui.Controls.FluentWindow`
* **Access Modifier:** `public partial`

---

## Fields & Properties

### Fields
* `private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;`
  * Reference to the default application settings object used for retrieving and persisting user preferences.

* `private ICollectionView _leftListView`
  * Provides filtering capabilities for the list of available (left list) buttons.

### Properties
* `private ObservableCollection<ButtonInfo> ButtonsInLeftList { get; set; }`
  * Collection containing the available buttons that are currently not included in the custom bottom bar layout.
* `private ObservableCollection<ButtonInfo> ButtonsInRightList { get; set; }`
  * Collection containing the buttons currently selected to appear in the custom bottom bar.

---

## Constructor

### `BottomBarSettings()`

Initializes a new instance of the `BottomBarSettings` class and performs the following setup operations:

1. **Component Initialization:** Calls `InitializeComponent()`.
2. **Copilot+ Capability Check:** Calls `WindowsAiUtilities.CanDeviceUseWinAI()` to check if the host device supports WinAI/Copilot+ features.
3. **Button Filtering & Population:**
   * Populates `ButtonsInRightList` with buttons retrieved from `CustomBottomBarUtilities.GetCustomBottomBarItemsSetting()`, filtering out buttons requiring Copilot+ if the device does not support it.
   * Binds `RightListBox.ItemsSource` to `ButtonsInRightList`.
   * Filters the master list `ButtonInfo.AllButtons` based on Copilot+ capabilities and removes any buttons that are already present in `ButtonsInRightList`.
   * Binds `LeftListBox.ItemsSource` to `ButtonsInLeftList`.
   * Retrieves the default collection view for `ButtonsInLeftList` and assigns it to `_leftListView`.
4. **Setting Toggle States:** Loads existing settings from `DefaultSettings` and initializes the following UI control states:
   * `ShowCursorTextCheckBox.IsChecked` (`DefaultSettings.ShowCursorText`)
   * `ShowScrollbarCheckBox.IsChecked` (`DefaultSettings.ScrollBottomBar`)
   * `ShowLanguagePickerToggle.IsChecked` (`DefaultSettings.EtwShowLangPicker`)
   * `ShowWordCountToggle.IsChecked` (`DefaultSettings.EtwShowWordCount`)
   * `ShowCharDetailsToggle.IsChecked` (`DefaultSettings.EtwShowCharDetails`)
   * `ShowMatchCountToggle.IsChecked` (`DefaultSettings.EtwShowMatchCount`)
   * `ShowRegexPatternToggle.IsChecked` (`DefaultSettings.EtwShowRegexPattern`)
   * `ShowSimilarMatchesToggle.IsChecked` (`DefaultSettings.EtwShowSimilarMatches`)

---

## Methods

### Static Helper Methods

#### `InsertSorted<T>(ObservableCollection<T> collection, T item, Func<T, double> propertySelector)`
Inserts an item into an `ObservableCollection<T>` while maintaining ascending order based on a specified numerical property selector function.

* **Parameters:**
  * `collection`: Target `ObservableCollection<T>`.
  * `item`: The element to insert.
  * `propertySelector`: Function extracting the `double` key value used for comparison (`p.OrderNumber`).

#### `MoveDown<T>(ObservableCollection<T> collection, int index)`
Moves an item at the specified `index` down one position in the collection.

* **Parameters:**
  * `collection`: Target `ObservableCollection<T>`.
  * `index`: Current index of the item to move.
* **Returns:** `int` — The new index of the item, or `collection.Count` if invalid/unmoved.

#### `MoveUp<T>(ObservableCollection<T> collection, int index)`
Moves an item at the specified `index` up one position in the collection.

* **Parameters:**
  * `collection`: Target `ObservableCollection<T>`.
  * `index`: Current index of the item to move.
* **Returns:** `int` — The new index of the item, or `0` if invalid/unmoved.

---

### Event Handlers

#### `CloseBTN_Click(object sender, RoutedEventArgs e)`
Closes the `BottomBarSettings` window without applying unsaved changes.

#### `FilterSearchBox_TextChanged(object sender, TextChangedEventArgs e)`
Filters the available buttons list (`_leftListView`) as the user types in the filter search box. Matches against `ButtonInfo.ButtonText` using case-insensitive comparison (`StringComparison.OrdinalIgnoreCase`).

#### `MoveDownButton_Click(object sender, RoutedEventArgs e)`
Invokes `MoveDown` on `ButtonsInRightList` using the currently selected index in `RightListBox`, then updates `RightListBox.SelectedIndex` to reflect the move.

#### `MoveLeftButton_Click(object sender, RoutedEventArgs e)`
Transfers the selected `ButtonInfo` item from `ButtonsInRightList` back to `ButtonsInLeftList`. The item is re-inserted into `ButtonsInLeftList` in sorted order based on `OrderNumber`.

#### `MoveRightButton_Click(object sender, RoutedEventArgs e)`
Transfers the selected `ButtonInfo` item from `ButtonsInLeftList` into `ButtonsInRightList`, adding it to the end of the collection.

#### `MoveUpButton_Click(object sender, RoutedEventArgs e)`
Invokes `MoveUp` on `ButtonsInRightList` using the currently selected index in `RightListBox`, then updates `RightListBox.SelectedIndex` to reflect the move.

#### `SaveBTN_Click(object sender, RoutedEventArgs e)`
Persists all configured settings and updates active windows:
1. Updates settings values in `DefaultSettings` based on the check/toggle states:
   * `ShowCursorText`
   * `ScrollBottomBar`
   * `EtwShowLangPicker`
   * `EtwShowWordCount`
   * `EtwShowCharDetails`
   * `EtwShowMatchCount`
   * `EtwShowRegexPattern`
   * `EtwShowSimilarMatches`
2. Saves configuration to disk via `DefaultSettings.Save()`.
3. Saves custom bottom bar items via `CustomBottomBarUtilities.SaveCustomBottomBarItemsSetting(...)`.
4. If the owner window (`Owner`) is an instance of `EditTextWindow`, invokes `etw.SetBottomBarButtons()` to immediately reflect updates in the parent window.
5. Closes the settings window.

---

## Control Dependencies & UI Elements

The code directly interacts with the following named UI elements (defined in XAML):

* **List Controls:** `LeftListBox`, `RightListBox`
* **Input Controls:** `FilterSearchBox`
* **Action Buttons:** `CloseBTN`, `SaveBTN`, `MoveLeftButton`, `MoveRightButton`, `MoveUpButton`, `MoveDownButton`
* **Toggle / CheckBox Controls:**
  * `ShowCursorTextCheckBox`
  * `ShowScrollbarCheckBox`
  * `ShowLanguagePickerToggle`
  * `ShowWordCountToggle`
  * `ShowCharDetailsToggle`
  * `ShowMatchCountToggle`
  * `ShowRegexPatternToggle`
  * `ShowSimilarMatchesToggle`