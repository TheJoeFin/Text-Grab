# Technical Documentation: `WindowsAiUtilities.cs`

## Overview

The `WindowsAiUtilities` static class in the `Text_Grab.Utilities` namespace serves as a bridge between the Text-Grab application and Windows App SDK on-device AI capabilities (`Microsoft.Windows.AI`). It provides wrappers for Optical Character Recognition (OCR), AI-driven image description generation, text summarization, text rewriting, text-to-table conversion, translation, and regular expression extraction.

---

## Key Dependencies & Namespaces

* **`Microsoft.Windows.AI`**: Core namespace for Windows AI features.
* **`Microsoft.Windows.AI.Text`**: Handles text-based AI operations (`TextRecognizer`, `TextSummarizer`, `TextRewriter`, `TextToTableConverter`, `LanguageModel`).
* **`Microsoft.Windows.AI.Imaging`**: Handles visual AI operations (`ImageDescriptionGenerator`, `ImageBuffer`).
* **`Microsoft.Windows.AI.ContentSafety`**: Configures content filtering severity levels (`ContentFilterOptions`, `SeverityLevel`).
* **`Windows.Graphics.Imaging` & `Microsoft.Graphics.Imaging`**: Manages bitmap representations (`SoftwareBitmap`).

---

## Fields & Constants

| Identifier | Type | Description |
| :--- | :--- | :--- |
| `TranslationPromptTemplate` | `const string` | Prompt template used to instruct the AI to perform text translation. |
| `_translationLanguageModel` | `LanguageModel?` | Shared static instance of `LanguageModel` reused across translation operations. |
| `_modelInitializationLock` | `SemaphoreSlim` | Thread-synchronization primitive (`1, 1`) used to initialize `_translationLanguageModel` safely. |
| `_disposed` | `bool` | Flag tracking whether the class static resources have been cleaned up. |
| `LanguageCodeMap` | `Dictionary<string, string>` | Case-insensitive mapping from display language names (e.g., "English", "Japanese") to standard ISO/BCP language codes (e.g., "en", "ja"). |

---

## System Requirements & Capabilities

Windows AI features are guarded by prerequisite checks before execution.

### Prerequisites Verification
* **`MeetsWindowsAiPrerequisites()`**:
  1. Verifies the application is packaged (`AppUtilities.IsPackaged()`).
  2. Ensures the OS is not Windows 10 (`!OSInterop.IsWindows10()`).
  3. Checks that the current system architecture is ARM64 (`RuntimeInformation.ProcessArchitecture == Architecture.Arm64`), unless overridden by user settings (`Settings.Default.OverrideAiArchCheck`).

### Capability Checks
* **`CanDeviceUseWinAI()`**: Checks if the text recognition feature readiness state is supported on the system (`TextRecognizer.GetReadyState`).
* **`CanDeviceDescribeImagesWithWinAI()`**: Checks if the image description feature readiness state is supported on the system (`ImageDescriptionGenerator.GetReadyState`).
* **`CanDeviceUseWinAiFeature(Func<AIFeatureReadyState> getReadyState)`**: Generic helper executing the prerequisite checks and evaluating feature readiness.

---

## Functional Components

### 1. Optical Character Recognition (OCR)

Extracts text from images using the `TextRecognizer` API.

* **`GetTextWithWinAI(string imagePath)`**:
  * Converts an image at `imagePath` to a `SoftwareBitmap` and wraps it in an `ImageBuffer`.
  * Calls `TextRecognizer.RecognizeTextFromImage()` and aggregates recognized text lines into a single string.
  * Ensures the `TextRecognizer` model is ready via `EnsureReadyAsync()` if in `NotReady` state.

* **`GetOcrResultAsync(Bitmap bmp)` / `GetOcrResultAsync(SoftwareBitmap softwareBitmap)`**:
  * Overloads that accept bitmap inputs and return structured OCR output wrapped in a `WinAiOcrLinesWords` object or raw `RecognizedText`.
  * Converts `Bitmap` inputs via a temporary PNG file to produce a valid `SoftwareBitmap`.

---

### 2. Image Description Generation

Generates natural language descriptions of visual content using `ImageDescriptionGenerator`.

* **`GetTextDescriptionWithWinAI(...)` Overloads**:
  * Accepts an image via file path (`string`), System.Drawing `Bitmap`, or WinRT `SoftwareBitmap`.
  * Supports cancellation tokens (`CancellationToken`) to halt on-device inference upon user cancellation.
  * **Content Safety**: Configures `ContentFilterOptions` with `ResponseMaxAllowedSeverityLevel` set to `SeverityLevel.Medium` for Self-Harm and Violent categories.
  * Requests accessibility descriptions using `ImageDescriptionKind.AccessibleDescription`.

---

### 3. Text Operations

Utilizes on-device `LanguageModel` implementations to analyze or modify text string content.

* **`SummarizeParagraph(string textToSummarize)`**:
  * Uses `TextSummarizer` with a `LanguageModel` instance to summarize long paragraphs.
  * Returns the summarized string or an error message prefixed with `ERROR:`.

* **`Rewrite(string textToRewrite)`**:
  * Uses `TextRewriter` with a `LanguageModel` instance to rephrase input text.

* **`TextToTable(string textToTable)`**:
  * Uses `TextToTableConverter` to convert unstructured text content into tabular data structures (`TextToTableRow`).
  * Returns the extracted rows formatted as tab-separated values (`\t`).

---

### 4. Language Translation & Heuristics

Performs text translation using a custom prompt sent to `TextRewriter`.

* **`IsLikelyInTargetLanguage(string text, string targetLanguage)`**:
  * Performs fast script detection using Unicode character range checks (CJK ideographs, Arabic, Cyrillic, Devanagari, Latin).
  * Includes word-frequency checks for English (checking common words like `" the "`, `" and "`, etc.).
  * Used as a pre-check in `TranslateText()` to bypass API calls if the source text is already in the target language script.

* **`TranslateText(string textToTranslate, string targetLanguage)`**:
  * Checks device readiness and performs heuristic detection via `IsLikelyInTargetLanguage`.
  * Initializes or reuses `_translationLanguageModel`.
  * Formats the text into `TranslationPromptTemplate` and passes it to `TextRewriter`.
  * Cleans up the response via `CleanTranslationResult` to strip out AI instruction echoes (e.g., "translation:", "here is the translation").

---

### 5. Regular Expression Extraction

Extracts standard regex patterns from descriptive text inputs.

* **`ExtractRegex(string textDescription)`**:
  * Sends a prompt requesting *only* the regex pattern corresponding to `textDescription`.
  * Cleans the model output using `CleanRegexResult`.

* **`CleanRegexResult(string regexText)`**:
  * Strips markdown code fences (```), backticks, comments (`//`, `#`), and prefixes (`regex:`, `pattern:`).
  * Identifies and returns the line containing valid regex structural characters (`[`, `(`, `\`, `^`, `$`, `+`, `*`, `?`, `|`, `.`).

---

## Resource Lifecycle & Cleanup

Because static `LanguageModel` and `SemaphoreSlim` instances hold native on-device resources, proper disposal is required:

* **`DisposeTranslationModel()`**: Disposes `_translationLanguageModel` and sets the reference to `null`.
* **`Cleanup()`**: Performs full static resource cleanup when the application shuts down. Disposes the translation model and `_modelInitializationLock`, updating `_disposed` to `true`.