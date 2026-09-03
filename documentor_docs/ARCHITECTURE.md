# Text-Grab Architectural Documentation

This document provides a comprehensive architectural overview of the **Text-Grab** codebase. The architecture is structured based on the component dependency graph and entity relations within the project.

---

## 1. Executive Summary & Core Responsibilities

**Text-Grab** is a Windows desktop application (built using WPF and .NET) designed for screen text recognition (OCR), quick text searching/lookup, text editing, and post-processing automation.

### Primary Systems:
1. **Presentation Layer (`Text-Grab/Views/`, `Text-Grab/Controls/`, `Text-Grab/Pages/`)**: Manages the application UI, window layouts, overlay framing, settings screens, and custom UI components (e.g., word borders, inline pickers, search bars).
2. **Core Services (`Text-Grab/Services/`)**: Handles application state persistence, settings storage, history logging, language management, text-to-speech synthesis, and mathematical/unit calculations.
3. **Execution & Utility Engine (`Text-Grab/Utilities/`)**: Orchestrates OCR engines (Windows AI, WinRT OCR, Tesseract), regular expression pattern matching, screen capture with HDR support, file/IO operations, and post-grab action execution.
4. **Data Models & State (`Text-Grab/Models/`, `Text-Grab/UndoRedoOperations/`)**: Represents OCR output structs, capture results, table state, templates, history records, and undo/redo operation stacks.
5. **System Interop (`Text-Grab/OSInterop.cs`, `Text-Grab/NativeMethods.cs`, `Text-Grab/DesktopNotificationManagerCompat.cs`)**: Interfaces directly with Windows APIs, P/Invoke hooks, notifications, display parameters, and hotkeys.
6. **Automation & Test Infrastructure (`UiTests/`, `Tests/`)**: Comprehensive test suite including unit tests, integration helpers, and UI automation test hosts.

---

## 2. High-Level Architecture Diagram

The diagram below illustrates the architectural layers and core dependency flows within the application.

```mermaid
graph TD
    subgraph Presentation [Presentation Layer (Views, Controls & Pages)]
        AppEntry[Text-Grab/App.xaml.cs]
        GrabFrame[Views/GrabFrame.xaml.cs]
        FullscreenGrab[Views/FullscreenGrab.xaml.cs]
        EditTextWindow[Views/EditTextWindow.xaml.cs]
        QuickLookup[Views/QuickSimpleLookup.xaml.cs]
        SettingsWindow[Views/SettingsWindow.xaml.cs]
        NotifyIconWin[Controls/NotifyIconWindow.xaml.cs]
        CustomControls[Controls/*]
        SettingsPages[Pages/*]
    end

    subgraph Services [Application Services]
        HistorySvc[Services/HistoryService.cs]
        SettingsSvc[Services/SettingsService.cs]
        LangSvc[Services/LanguageService.cs]
        TtsSvc[Services/TtsService.cs]
        CalcSvc[Services/CalculationService.cs]
    end

    subgraph Operations [Undo / Redo Framework]
        UndoRedo[UndoRedoOperations/UndoRedo.cs]
        OperationsList[UndoRedoOperations/Operation.cs]
    end

    subgraph Executors [Executors & Core Logic]
        OcrUtils[Utilities/OcrUtilities.cs]
        GrabTemplateExec[Utilities/GrabTemplateExecutor.cs]
        PatternExec[Utilities/PatternExecutor.cs]
        RecognizerExec[Utilities/RecognizerExecutor.cs]
        PostGrabMgr[Utilities/PostGrabActionManager.cs]
        WinAiUtils[Utilities/WindowsAiUtilities.cs]
        TessHelper[Utilities/TesseractHelper.cs]
        ImageMethods[Utilities/ImageMethods.cs]
    end

    subgraph DomainModels [Models & Persistence]
        HistoryInfo[Models/HistoryInfo.cs]
        GrabTemplate[Models/GrabTemplate.cs]
        EditTextDoc[Models/EditTextTableDocument.cs]
        WordBorderInfo[Models/WordBorderInfo.cs]
        SettingsStore[Properties/Settings.Designer.cs]
        JsonUtil[Utilities/Json.cs]
    end

    subgraph OSIntegration [OS Interop & System Native]
        OSInterop[OSInterop.cs]
        NativeMethods[NativeMethods.cs]
        HotKeys[Utilities/HotKeyManager.cs]
        DesktopNotif[DesktopNotificationManagerCompat.cs]
    end

    subgraph TestSuite [Testing Infrastructure]
        UnitTests[Tests/*]
        UiAutomationHost[UiTests/TextGrab.AutomationHost]
        SysIntegrationHelper[UiTests/TextGrab.SystemIntegrationHelper]
    end

    %% Key Relationships
    AppEntry --> GrabFrame
    AppEntry --> SettingsWindow
    AppEntry --> HistorySvc

    GrabFrame --> OcrUtils
    GrabFrame --> HistorySvc
    GrabFrame --> ImageMethods
    GrabFrame --> GrabTemplateExec
    GrabFrame --> UndoRedo

    EditTextWindow --> CalcSvc
    EditTextWindow --> OcrUtils
    EditTextWindow --> HistorySvc
    EditTextWindow --> PatternExec

    PostGrabMgr --> GrabTemplateExec
    PostGrabMgr --> WinAiUtils
    PostGrabMgr --> TtsSvc

    OcrUtils --> WinAiUtils
    OcrUtils --> TessHelper
    OcrUtils --> ImageMethods

    HistorySvc --> HistoryInfo
    HistorySvc --> JsonUtil

    SettingsSvc --> SettingsStore
    SettingsSvc --> JsonUtil

    WinAiUtils --> OSInterop
    ImageMethods --> OSInterop

    UnitTests --> Presentation
    UnitTests --> Services
    UnitTests --> Executors
    UnitTests --> DomainModels
    UiAutomationHost --> SysIntegrationHelper
```

---

## 3. Subsystem Breakdown

### 3.1 Presentation Layer (`Text-Grab/Views/`, `Controls/`, `Pages/`)
* **Views**: Primary user interfaces.
  * `GrabFrame.xaml.cs`: Scalable framing window for real-time and region captures.
  * `FullscreenGrab.xaml.cs` & `FullscreenGrab.SelectionStyles.cs`: Full-screen overlay capture screen.
  * `EditTextWindow.xaml.cs`: Editor for viewing, formatting, searching, splitting, and processing extracted text/tables.
  * `QuickSimpleLookup.xaml.cs`: Quick search dialog for history and regex patterns.
  * `SettingsWindow.xaml.cs` & `FirstRunWindow.xaml.cs`: Application setup and configuration screens.
  * `LicensesWindow.xaml.cs`: Displays third-party licenses.
* **Controls**: Specialized WPF controls such as `WordBorder.xaml.cs`, `FindAndReplaceWindow.xaml.cs`, `RegexManager.xaml.cs`, `PostGrabActionEditor.xaml.cs`, `SplitColumnWindow.xaml.cs`, `NotifyIconWindow.xaml.cs`, and `InlinePickerRichTextBox.cs`.
* **Pages**: Tabbed configuration views hosted inside `SettingsWindow` (e.g., `GeneralSettings`, `LanguageSettings`, `KeysSettings`, `TesseractSettings`, `FullscreenGrabSettings`, `EditTextWindowSettings`, `VoiceOutputSettings`, `DangerSettings`).

### 3.2 Services Layer (`Text-Grab/Services/`)
* **`HistoryService`**: Reads, updates, and persists grab history to JSON storage.
* **`SettingsService`**: Interfaces with `Settings.Designer.cs` and handles application configuration persistence.
* **`LanguageService`**: Manages installed language packages across Windows AI, WinRT OCR, and Tesseract.
* **`TtsService` & `WindowsSpeechEngine`**: Provides Text-to-Speech execution implementing `ITtsEngine`.
* **`CalculationService`** (split across `.cs`, `.DateTimeMath.cs`, `.UnitMath.cs`): Performs inline string math and unit/date conversions.

### 3.3 Core Executors & Utilities (`Text-Grab/Utilities/`)
* **OCR Utilities**:
  * `OcrUtilities.cs`: Aggregates OCR logic across WinRT, Windows AI (`WindowsAiUtilities.cs`), and Tesseract (`TesseractHelper.cs`).
  * `PdfDocumentRenderer.cs`: Renders and processes text overlays from PDF source pages.
* **Template & Pattern Execution**:
  * `GrabTemplateExecutor.cs` & `GrabTemplateManager.cs`: Executes pattern matching across defined screen regions.
  * `PatternExecutor.cs` & `RecognizerExecutor.cs`: Applies built-in recognizers and regular expressions to text results.
  * `PostGrabActionManager.cs`: Dispatches automated actions following a grab operation.
* **Screen & Image Capture**:
  * `ImageMethods.cs`, `FreeformCaptureUtilities.cs`, `WindowSelectionUtilities.cs`, `CursorClipper.cs`.
  * `HdrScreenCapture.cs`, `DisplayHdrInfo.cs`, `HdrToneMapper.cs`: Provides screen capture support for HDR configurations.
* **System & Automation Infrastructure**:
  * `AppUtilities.cs`, `FileUtilities.cs`, `IoUtilities.cs`, `NotificationUtilities.cs`, `ClipboardUtilities.cs`, `HotKeyManager.cs`, `ProtocolUtilities.cs`, `AutomationProfile.cs`, `AutomationDiagnostics.cs`, `AutomationSettingsProvider.cs`.

### 3.4 Domain Models (`Text-Grab/Models/` & `Interfaces/`)
* **Core Models**: `HistoryInfo`, `GrabTemplate`, `WordBorderInfo`, `EditTextTableDocument`, `ResultTable`, `ShortcutKeySet`, `ButtonInfo`, `PatternItem`, `StoredRegex`, `ExtractedPattern`.
* **OCR Models**: `OcrLinesWords`, `WinRtOcrLinesWords`, `WinAiOcrLinesWords`, `GeneratedOcrLinesWords`, `OcrOutput`, `AsyncOcrFileResult`.
* **Language Models**: `GlobalLang`, `TessLang`, `WindowsAiLang`, `WindowsAiDescriptionLang`, `UiAutomationLang`.
* **Interfaces**: `ILanguage`, `ITtsEngine`.

### 3.5 Undo / Redo Operations (`Text-Grab/UndoRedoOperations/`)
Implements the Command pattern for tracking modifications inside capture regions and word borders:
* `UndoRedo.cs`: Controller class managing undo/redo stacks.
* `Operation.cs`: Abstract base operation.
* Concrete operations: `AddWordBorder.cs`, `RemoveWordBorder.cs`, `ResizeWordBorder.cs`, `ChangeWord.cs`, `ChangedImage.cs`.

### 3.6 Platform Interop (`Text-Grab/`)
* `OSInterop.cs` & `NativeMethods.cs`: P/Invoke declarations for Windows OS interactions (window positioning, monitor details, cursor manipulation).
* `DesktopNotificationManagerCompat.cs` & `TextGrabNotificationActivator.cs`: Integration with Windows Toast Notification System.
* `WPFExtensionMethods.cs`: Extension methods for WPF framework element handling.

### 3.7 Extensions (`Text-Grab/Extensions/`)
Utility extensions extending built-in types:
* `ImageExtensions.cs`, `SoftwareBitmapExtensions.cs`, `StorageFileExtensions.cs`
* `KeyboardExtensions.cs`, `ControlExtensions.cs`, `ShapeExtensions.cs`
* `LanguageExtensions.cs`, `StringBuilderExtensions.cs`, `NumberExtensions.cs`, `DapploExtensions.cs`, `SettingsStorageExtensions.cs`.

### 3.8 Test & UI Automation Infrastructure (`Tests/`, `UiTests/`)
* **Unit & Integration Tests (`Tests/`)**: Covers utilities, OCR processing, history storage, calculators, layout engines, and window states (e.g., `OcrTests.cs`, `HistoryServiceTests.cs`, `CalculatorTests.cs`, `GrabTemplateExecutorTests.cs`, `UiAutomationContractTests.cs`).
* **Automation Host (`UiTests/TextGrab.AutomationHost/`)**: Standalone host application (`App`, `MainWindow`, `FixtureStateWriter`, `FixtureOptions`) for UI automation test scenarios.
* **System Integration Helper (`UiTests/TextGrab.SystemIntegrationHelper/`)**: Console application helper for system-level automation testing.

---

## 4. Key Architectural Design Patterns

1. **Singleton Pattern**:
   * Enforced via `Text-Grab/Utilities/Singleton.cs` for cross-cutting instances like app utilities, settings controllers, and language management.
2. **Command Pattern (Undo/Redo)**:
   * Encapsulated under `Text-Grab/UndoRedoOperations/`. Tracks user adjustments to OCR bounding boxes and text blocks.
3. **Strategy / Executor Pattern**:
   * Utilized in text processing (`PatternExecutor`, `RecognizerExecutor`, `GrabTemplateExecutor`) to dynamically select regular expressions, built-in recognizers, or region templates.
4. **Adapter / Interop Bridge**:
   * Managed by `OcrUtilities.cs` to unify different underlying OCR platforms (WinRT, Windows AI, Tesseract) behind a standardized interface.
5. **Provider Pattern**:
   * `AutomationSettingsProvider.cs` supplies dynamic runtime configuration parameters to the automation engine without modifying application core settings directly.