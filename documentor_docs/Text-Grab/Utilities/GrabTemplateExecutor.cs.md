# Technical Documentation: `GrabTemplateExecutor.cs`

## Overview

The `GrabTemplateExecutor` class in `Text_Grab.Utilities` is a static helper class responsible for executing structured `GrabTemplate` models against captured screen regions, image bitmaps, or plain text strings. 

It orchestrates the Optical Character Recognition (OCR) process across defined sub-regions, applies Regular Expression (Regex) pattern matching, invokes built-in recognizers, and formats final output strings according to template placeholders.

---

## Template Placeholder Syntax

`GrabTemplateExecutor` parses four categories of placeholders and escape sequences within a template's `OutputTemplate` string:

### 1. Region Placeholders
Region placeholders extract OCR text from specific, 1-based region numbers defined within the template.
* `{N}`: Extracts raw OCR text from region `N`.
* `{N:trim}`: Trims leading and trailing whitespace from region `N` OCR text.
* `{N:upper}`: Converts region `N` OCR text to uppercase.
* `{N:lower}`: Converts region `N` OCR text to lowercase.

### 2. Pattern Placeholders
Pattern placeholders perform regex matching over the full captured text using saved regular expressions.
* **Format**: `{p:PatternName:mode}` or `{p:PatternName:mode:separator}`
* **Examples**:
  * `{p:Email:first}`: Extracts the first regex match for the pattern "Email".
  * `{p:Phone:last}`: Extracts the last regex match for the pattern "Phone".
  * `{p:Code:all:, }`: Joins all matches using the separator `, `.
  * `{p:Number:2}`: Extracts the 2nd match (1-based index).
  * `{p:Item:1,3}`: Extracts the 1st and 3rd matches joined by the default or explicit separator.

### 3. Recognizer Placeholders
Recognizer placeholders pass full captured text to built-in recognizers.
* **Format**: `{r:RecognizerName:mode}` or `{r:RecognizerName:mode:outputKind:separator}`
* **`outputKind`**:
  * `value` (default): Returns the parsed/resolved value.
  * `text`: Returns the exact matched text string.

### 4. Escape Sequences
* `\n`: Replaced with a newline character.
* `\t`: Replaced with a tab character.
* `\\`: Replaced with a literal backslash `\`.
* `\{`: Replaced with a literal opening brace `{`.

---

## Key Components & Regular Expressions

The class defines the following compiled regex constants and timeouts:

* **`PlaceholderRegex`**: `\{(\d+)(?::([a-z]+))?\}`
  * Matches region placeholders (e.g., `{1}`, `{2:upper}`).
* **`PatternPlaceholderRegex`**: `\{p:([^:}]+):([^:}]+)(?::([^}]*))?\}`
  * Matches pattern placeholders (Group 1: Pattern Name, Group 2: Mode, Group 3: Optional Separator).
* **`RecognizerPlaceholderRegex`**: `\{r:([^:}]+):([^:}]+)(?::(value|text))?(?::([^}]*))?\}`
  * Matches recognizer placeholders (Group 1: Recognizer Name, Group 2: Mode, Group 3: Output Kind, Group 4: Optional Separator).
* **`RegexTimeout`**: `TimeSpan.FromSeconds(5)`
  * Enforces a 5-second execution limit on pattern regex matches to prevent Denial of Service via complex regular expressions.

---

## Public API Reference

### 1. `ExecuteTemplateAsync`

```csharp
public static async Task<string> ExecuteTemplateAsync(
    GrabTemplate template,
    Rect captureRegion,
    ILanguage? language = null)
```

Executes a template against a physical screen coordinate bounding box (`Rect`).

#### Execution Flow:
1. Validates `template.IsValid`. Returns `string.Empty` if invalid.
2. Resolves `ILanguage` using `language` or falls back to `LanguageUtilities.GetOCRLanguage()`.
3. Calls `OcrAllRegionsAsync` if regions exist in `template.Regions`.
4. Checks if full-area OCR is necessary (if `PatternMatches`, `RecognizerMatches`, or corresponding placeholder syntaxes are detected in `OutputTemplate`).
5. If required, performs full-area OCR via `OcrUtilities.GetTextFromAbsoluteRectAsync(captureRegion, resolvedLanguage)`.
6. Resolves regex strings using `ResolvePatternRegexes`.
7. Assembles output by executing `ApplyOutputTemplate`, `ApplyPatternPlaceholders`, and `ApplyRecognizerPlaceholders`.

---

### 2. `ExecuteTemplateOnBitmapAsync`

```csharp
public static async Task<string> ExecuteTemplateOnBitmapAsync(
    GrabTemplate template,
    Bitmap bitmap,
    ILanguage? language = null)
```

Executes a template against an in-memory `System.Drawing.Bitmap`.

#### Execution Flow:
1. Validates `template.IsValid`.
2. Resolves OCR language.
3. For each sub-region in `template.Regions`:
   * Calculates pixel coordinates and dimensions using relative ratios (`RatioLeft`, `RatioTop`, `RatioWidth`, `RatioHeight`).
   * Clamps dimensions to bitmap bounds.
   * Crops sub-bitmaps, runs `OcrUtilities.GetTextFromImageAsync`, and formats results.
   * If width or height is `<= 0`, or an exception occurs, assigns `region.DefaultValue`.
4. Executes full-bitmap OCR if pattern or recognizer placeholders exist.
5. Applies region, pattern, and recognizer placeholders to format the final string.

---

### 3. `ApplyTextOnlyTemplate`

```csharp
public static string ApplyTextOnlyTemplate(GrabTemplate template, string text)
```

Processes existing text synchronously without invoking OCR engines.

* Region placeholders resolve to empty strings because no region OCR is executed.
* Parses and applies pattern placeholders (`ApplyPatternPlaceholders`) and recognizer placeholders (`ApplyRecognizerPlaceholders`) directly against the provided `text`.

---

### 4. `ApplyOutputTemplate`

```csharp
public static string ApplyOutputTemplate(
    string outputTemplate,
    IReadOnlyDictionary<int, string> regionResults)
```

Replaces escape sequences and region placeholders (`{N}`, `{N:modifier}`) using the provided `regionResults` dictionary.

* Escapes literal backslashes and braces using sentinel markers (`\x00BACKSLASH\x00`, `\x00LBRACE\x00`) to prevent conflicting substitutions during token replacement.
* Unrecognized placeholders are left intact.

---

### 5. `ApplyPatternPlaceholders`

```csharp
public static string ApplyPatternPlaceholders(
    string template,
    string fullText,
    IReadOnlyList<TemplatePatternMatch> patternMatches,
    IReadOnlyDictionary<string, string> patternRegexes)
```

Finds all `{p:PatternName:mode[:separator]}` matches within `template` and substitutes them with extracted regex values from `fullText`.

* Catches `RegexMatchTimeoutException` and `ArgumentException` (invalid regex), returning `string.Empty` for failed pattern evaluations.

---

### 6. `ParsePatternMatchesFromOutputTemplate`

```csharp
public static List<TemplatePatternMatch> ParsePatternMatchesFromOutputTemplate(string outputTemplate)
```

Parses `{p:PatternName:mode[:separator]}` tokens out of an `outputTemplate` string and cross-references them with saved regex configurations loaded from `AppUtilities.TextGrabSettingsService.LoadStoredRegexes()`.

---

### 7. `ApplyRecognizerPlaceholders`

```csharp
public static string ApplyRecognizerPlaceholders(string template, string fullText)
```

Replaces `{r:RecognizerName:mode[:outputKind][:separator]}` placeholders by retrieving a `BuiltInRecognizer` instance via `BuiltInRecognizer.GetByName()` and executing `RecognizerExecutor.ApplyRecognizer(...)`.

---

### 8. `ParseRecognizerMatchesFromOutputTemplate`

```csharp
public static List<TemplateRecognizerMatch> ParseRecognizerMatchesFromOutputTemplate(string outputTemplate)
```

Parses `{r:...}` placeholders directly from an output template and maps them into `TemplateRecognizerMatch` objects.

---

### 9. `ValidateOutputTemplate`

```csharp
public static List<string> ValidateOutputTemplate(
    string outputTemplate,
    IEnumerable<int> availableRegionNumbers,
    IEnumerable<string>? availablePatternNames = null)
```

Validates output template syntax before execution and returns a list of error strings.

#### Checked Validation Rules:
* **Invalid Region Format**: Flags invalid syntax in region placeholders.
* **Missing Region**: Flags placeholders referencing region IDs that do not exist in `availableRegionNumbers`.
* **Unused Defined Region**: Warns if a defined region is not referenced anywhere in the template.
* **Missing Pattern**: Flags pattern placeholders referencing names missing from `availablePatternNames`.
* **Invalid Match Mode**: Validates mode strings using `IsValidMatchMode`.

---

## Internal Utility Logic

### Match Mode Extraction

`ExtractMatchesByMode` handles mode selection for pattern and recognizer matching:

```csharp
internal static string ExtractMatchesByMode(IReadOnlyList<string> allValues, string mode, string separator)
```

Supported modes:
* `"first"`: Returns `allValues[0]`.
* `"last"`: Returns `allValues[^1]`.
* `"all"`: Joins all elements in `allValues` using `separator`.
* Numeric Index / Indices (e.g., `"2"`, `"1,3,5"`): Evaluated via `ExtractByIndices`. Converts 1-based placeholder index values to 0-based array lookup. Out-of-bounds indices are ignored.

### Pattern Regex Resolution

`ResolvePatternRegexes` maps `TemplatePatternMatch` configurations to active regex pattern strings stored in application settings:
1. Loads patterns via `AppUtilities.TextGrabSettingsService.LoadStoredRegexes()`.
2. Uses default patterns (`StoredRegex.GetDefaultPatterns()`) if no saved patterns exist.
3. Attempts lookup by `PatternId` first (maintaining functionality if renamed), falling back to `PatternName`.

### Region OCR (`OcrAllRegionsAsync`)

For full screen region captures:
1. Computes absolute bounding boxes:
   $$x = \text{captureRegion.X} + (\text{RatioLeft} \times \text{captureRegion.Width})$$
   $$y = \text{captureRegion.Y} + (\text{RatioTop} \times \text{captureRegion.Height})$$
   $$\text{width} = \text{RatioWidth} \times \text{captureRegion.Width}$$
   $$\text{height} = \text{RatioHeight} \times \text{captureRegion.Height}$$
2. Runs `OcrUtilities.GetTextFromAbsoluteRectAsync`.
3. If OCR returns empty text or throws an exception, defaults to `region.DefaultValue`.