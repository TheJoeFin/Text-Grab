# Technical Documentation: `Text-Grab/Properties/Settings.Designer.cs`

## Overview

The `Settings.Designer.cs` file is an auto-generated C# code-behind file that defines the `Text_Grab.Properties.Settings` class. This class provides strongly typed access to user-scoped application settings for the **Text-Grab** application.

It inherits from `System.Configuration.ApplicationSettingsBase` and acts as a central configuration wrapper. Every configuration option defines a getter and setter that interfaces with the underlying C# settings architecture using indexed string keys (`this["PropertyName"]`).

---

## File Metadata

* **Namespace:** `Text_Grab.Properties`
* **Class Name:** `Settings`
* **Access Modifier:** `internal sealed partial`
* **Base Class:** `System.Configuration.ApplicationSettingsBase`
* **Generator Tool:** `Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator` (Version 18.9.0.0)
* **Target Framework Runtime:** `.NET Runtime v4.0.30319.42000`

---

## Class Architecture & Key Design Patterns

### 1. Generated Attributes
The class is decorated with compile-time attributes indicating its auto-generated status:
* `[CompilerGeneratedAttribute()]`: Informs the compiler that this code was automatically generated.
* `[GeneratedCodeAttribute(...)]`: Identifies the tool name and version used to produce the code.

### 2. Singleton Pattern
The class uses a thread-safe Singleton pattern to ensure a single shared instance throughout the application's lifespan:

```csharp
private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));

public static Settings Default {
    get {
        return defaultInstance;
    }
}
```

* **`Synchronized(...)`**: Wraps the instance creation in a thread-safe thread synchronization wrapper provided by `ApplicationSettingsBase`.
* **`Default` Property**: Provides global read-only access to the static default instance.

### 3. Property Attributes & Mechanics
Each property in the class follows a standard declaration pattern:
* `[UserScopedSettingAttribute()]`: Specifies that the property value is stored per-user (User Scope) rather than per-application.
* `[DebuggerNonUserCodeAttribute()]`: Tells debuggers to skip stepping into these property getters and setters.
* `[DefaultSettingValueAttribute("...") ]`: Specifies the hardcoded default value as a string representation.

Inside each property getter/setter, the underlying value is accessed through the `ApplicationSettingsBase` indexer:
```csharp
get {
    return ((<Type>)(this["<PropertyName>"]));
}
set {
    this["<PropertyName>"] = value;
}
```

---

## Property Catalog

Below is a complete inventory of all properties defined in `Settings.Designer.cs`, categorized by functional area.

### General & App Behavior
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `FirstRun` | `bool` | `True` | Flags whether the app is launching for the first time. |
| `ShowToast` | `bool` | `True` | Controls whether toast notifications are displayed. |
| `DefaultLaunch` | `string` | `"Fullscreen"` | Specifies the default view or mode on startup. |
| `RunInTheBackground` | `bool` | `False` | Toggles whether the application remains active in the background. |
| `StartupOnLogin` | `bool` | `False` | Toggles automatic application startup at Windows login. |
| `GlobalHotkeysEnabled` | `bool` | `True` | Enables or disables system-wide shortcut keys. |
| `AppTheme` | `string` | `"System"` | Visual theme setting (e.g., System, Light, Dark). |
| `UseHistory` | `bool` | `True` | Toggles saving history for captures. |
| `EnableFileBackedManagedSettings` | `bool` | `False` | Enables file-backed management for settings. |
| `AddToContextMenu` | `bool` | `False` | Integrates Text-Grab into Windows context menus. |
| `RegisterOpenWith` | `bool` | `False` | Registers Text-Grab in the system "Open With" dialog. |

### Hotkeys
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `FullscreenGrabHotKey` | `string` | `""` | Shortcut key sequence for Fullscreen Grab. |
| `GrabFrameHotkey` | `string` | `""` | Shortcut key sequence for Grab Frame. |
| `EditWindowHotKey` | `string` | `""` | Shortcut key sequence for opening the Edit Text Window. |
| `LookupHotKey` | `string` | `""` | Shortcut key sequence for triggering Lookup. |

### Edit Text Window (ETW) Settings
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `EditWindowStartFullscreen` | `bool` | `False` | Opens the Edit Text Window in fullscreen mode. |
| `FontFamilySetting` | `string` | `"Segoe UI"` | Font family used in text editors. |
| `FontSizeSetting` | `double` | `19` | Font size used in text editors. |
| `IsFontBold` | `bool` | `False` | Bold styling toggle. |
| `IsFontItalic` | `bool` | `False` | Italic styling toggle. |
| `IsFontUnderline` | `bool` | `False` | Underline styling toggle. |
| `IsFontStrikeout` | `bool` | `False` | Strikeout styling toggle. |
| `EditTextWindowSizeAndPosition` | `string` | `""` | Persisted window coordinates and dimensions for ETW. |
| `EditWindowIsWordWrapOn` | `bool` | `True` | Word wrap state for the editor. |
| `EditWindowIsOnTop` | `bool` | `False` | Toggles "Always on Top" for ETW. |
| `EditWindowBottomBarIsHidden` | `bool` | `False` | Visibility toggle for the bottom toolbar in ETW. |
| `RestoreEtwPositions` | `bool` | `True` | Restores prior window position on open. |
| `EtwUseMargins` | `bool` | `False` | Controls margin visibility/usage in the editor. |
| `EtwShowLangPicker` | `bool` | `False` | Controls language picker visibility. |
| `EtwShowWordCount` | `bool` | `True` | Displays word count in the editor status bar. |
| `EtwShowCharDetails` | `bool` | `False` | Displays extended character details. |
| `EtwShowMatchCount` | `bool` | `False` | Shows count of search/regex matches. |
| `EtwShowRegexPattern` | `bool` | `False` | Shows active regex pattern UI elements. |
| `EtwShowSimilarMatches` | `bool` | `False` | Shows similar string match results. |
| `EtwNormalizeLineEndingsOnPaste` | `bool` | `True` | Standardizes line breaks (`\r\n` vs `\n`) on paste operations. |
| `EtwSpellCheckMode` | `string` | `"Auto"` | Spell checking behavior (`Auto`, `On`, `Off`). |

### Grab Frame Mode
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `GrabFrameWindowSizeAndPosition`| `string` | `""` | Window bounds persistence string for Grab Frame. |
| `GrabFrameAutoOcr` | `bool` | `True` | Automatically runs OCR when Grab Frame region updates. |
| `GrabFrameUpdateEtw` | `bool` | `True` | Sends Grab Frame contents directly to Edit Text Window. |
| `GrabFrameScrollBehavior` | `string` | `"Resize"` | Controls action when mouse wheel is scrolled over frame. |
| `GrabFrameReadBarcodes` | `bool` | `True` | Enables barcode scanning in Grab Frame mode. |
| `GrabFrameTranslationEnabled` | `bool` | `False` | Auto-translates OCR text within Grab Frame. |
| `GrabFrameSpeakEnabled` | `bool` | `False` | Reads captured text aloud via TTS in Grab Frame. |
| `GrabFrameTranslationLanguage` | `string` | `"English"` | Target translation language for Grab Frame. |
| `GrabFrameWordGrouping` | `string` | `""` | Defines word grouping configuration/rules. |
| `GrabFrameHiddenBottomBarTools` | `string` | `""` | List of hidden toolbar icons/tools. |
| `GrabFrameBorderStyle` | `string` | `"Theme"` | Visual style of frame border. |
| `GrabFrameBorderColor` | `string` | `"#2A767E"` | Hex color string for border styling. |
| `CloseFrameOnGrab` | `bool` | `False` | Automatically closes Grab Frame after performing a grab. |

### Fullscreen Grab (FSG) Mode
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `FSGMakeSingleLineToggle` | `bool` | `False` | Merges captured multi-line text into a single line. |
| `FsgSendEtwToggle` | `bool` | `False` | Directs FSG text directly into ETW. |
| `FsgDefaultMode` | `string` | `"Default"` | Default interaction mode for Fullscreen Grab. |
| `FsgSelectionStyle` | `string` | `"Region"` | Selection visual style (`Region`, etc.). |
| `FsgShadeOverlay` | `bool` | `True` | Enables shaded background overlay during capture. |

### Text Processing & OCR Capabilities
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `CorrectErrors` | `bool` | `True` | Enables OCR error-correction heuristics. |
| `CorrectToLatin` | `bool` | `True` | Attempts to normalize recognized characters to Latin script. |
| `NeverAutoUseClipboard` | `bool` | `False` | Prevents automatically populating the system clipboard. |
| `TryInsert` | `bool` | `False` | Attempts to automatically paste text into active window. |
| `InsertDelay` | `double` | `2` | Delay in seconds prior to executing auto-insert. |
| `LastUsedLang` | `string` | `""` | Language code for the most recently selected OCR language. |
| `TryToReadBarcodes` | `bool` | `True` | Enables barcode detection during OCR grabs. |
| `UseTesseract` | `bool` | `False` | Enables Tesseract engine instead of native Windows OCR. |
| `TesseractPath` | `string` | `""` | File path to external Tesseract OCR binary. |
| `ParagraphDetection` | `bool` | `True` | Enables automatic detection and formatting of paragraphs. |
| `RemoveFurigana` | `bool` | `True` | Removes furigana reading aids when parsing Japanese text. |

### UI & Custom Action Persistence
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `BottomButtonsJson` | `string` | `""` | JSON storage for customized bottom bar buttons. |
| `ShowCursorText` | `bool` | `True` | Shows text tooltips near the mouse cursor. |
| `ScrollBottomBar` | `bool` | `True` | Allows horizontal scrolling on bottom toolbar. |
| `ShortcutKeySets` | `string` | `""` | JSON configuration of custom shortcut mappings. |
| `DefaultWebSearch` | `string` | `""` | Custom URL string for web search provider. |
| `WebSearchItemsJson` | `string` | `""` | JSON string defining custom web search destinations. |
| `RegexList` | `string` | `""` | JSON string of saved regular expressions. |
| `HiddenSmartPatternIds` | `string` | `""` | Filter list for hidden smart pattern definitions. |
| `PostGrabJSON` | `string` | `""` | JSON configuration for automated post-grab actions. |
| `PostGrabCheckStates` | `string` | `""` | State tracker for post-grab action checkmarks. |
| `PostGrabStayOpen` | `bool` | `False` | Keeps window active after post-grab pipeline runs. |
| `GrabTemplatesJSON` | `string` | `""` | Custom grab layout templates serialized as JSON. |

### Lookup Tool
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `LookupFileLocation` | `string` | `""` | Path to file consumed by the Lookup utility. |
| `LookupSearchHistory` | `bool` | `True` | Persists lookup query history. |

### UI Automation & AI Integration
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `OverrideAiArchCheck` | `bool` | `False` | Bypasses CPU architecture checks for AI functionality. |
| `WindowsAiDescriptionEnabled` | `bool` | `False` | Enables Windows Copilot/AI image description services. |
| `UiAutomationEnabled` | `bool` | `False` | Uses Windows UI Automation to pull text elements directly. |
| `UiAutomationFallbackToOcr` | `bool` | `True` | Falls back to OCR if UI Automation retrieves no elements. |
| `UiAutomationTraversalMode` | `string` | `"Balanced"`| UI tree traversal depth (`Balanced`, `Deep`, `Shallow`). |
| `UiAutomationIncludeOffscreen` | `bool` | `False` | Includes UI elements positioned outside the screen bounds. |
| `UiAutomationPreferFocusedElement`| `bool` | `True` | Focuses UI Automation extraction on active element. |

### Text-To-Speech (TTS)
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `SpeakProcessingStatus` | `bool` | `False` | Verbose voice output for background processing events. |
| `TtsSpeakWordLimit` | `int` | `100` | Maximum word count cap for TTS playback. |
| `TtsVoiceName` | `string` | `""` | Installed system TTS voice identifier. |
| `TtsSpeakingRate` | `double` | `1` | Speech synthesis rate multiplier. |

### Calculator Pane
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `CalcShowErrors` | `bool` | `False` | Toggles error rendering inside inline calculator pane. |
| `CalcShowPane` | `bool` | `False` | Visibility toggle for calculator side pane. |
| `CalcPaneWidth` | `int` | `400` | Width in pixels for the calculator pane. |

### Display & Screen Adjustments
| Property Name | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `HdrCaptureCorrection` | `bool` | `False` | Adjusts capture color space for High Dynamic Range displays. |
| `HdrBorderlessGranted` | `bool` | `False` | Permission flag for borderless capture techniques on HDR screens. |

---

## Modifying Settings

Because this file is **auto-generated by Visual Studio**, direct edits to `Settings.Designer.cs` will be overwritten when settings are updated through the IDE properties menu or when `Settings.settings` is re-compiled. 

To permanently alter settings or default values:
1. Open the project in Visual Studio.
2. Navigate to `Properties` -> `Settings.settings`.
3. Modify settings via the Visual Studio GUI editor.