# Technical Documentation: `GrabTemplateExecutorTests.cs`

## Overview

The `GrabTemplateExecutorTests.cs` file contains the unit test suite for the template execution engine in the **Text-Grab** application. Using the **xUnit** testing framework, this class validates the behaviors of `GrabTemplateExecutor` and the `GrabTemplate` data model.

The primary purpose of this test file is to ensure the accuracy and reliability of:
1. Template region substitution and formatting modifiers.
2. Escape sequence parsing (`\n`, `\t`, `\{`, `\\`).
3. Template validation logic for regions and pattern placeholders.
4. Regular expression pattern matching and placeholder resolution (`{p:PatternName:mode}`).
5. Match extraction modes (`first`, `last`, `all`, dynamic single/multiple indices).
6. Text-only template execution and edge-case fallbacks.
7. `GrabTemplate` model validation and property checks.

---

## File Details

- **File Path:** `Tests/GrabTemplateExecutorTests.cs`
- **Namespace:** `Tests`
- **Dependencies:**
  - `Text_Grab.Models`
  - `Text_Grab.Utilities`
  - `System.Text.RegularExpressions` (used within specific extraction tests)
  - `Xunit`

---

## Key Tested Components

The unit tests in this file target static methods on `GrabTemplateExecutor` and instance methods/properties on `GrabTemplate`:

### 1. `GrabTemplateExecutor.ApplyOutputTemplate`
Validates basic template region substitution using 1-based region numbers (e.g., `{1}`, `{2}`):
- **Region Substitution:** Replaces placeholders with values supplied in a `Dictionary<int, string>`.
- **Missing Regions:** Replaces references to missing region keys with empty strings.
- **Empty Templates:** Handled safely by returning an empty string.

### 2. Output Template Modifiers
Validates formatting modifiers appended to region indices using the `{index:modifier}` syntax:
- **`trim`:** Removes leading and trailing whitespace from the region string (`{1:trim}`).
- **`upper`:** Converts the region string to uppercase (`{1:upper}`).
- **`lower`:** Converts the region string to lowercase (`{1:lower}`).
- **Unknown Modifiers:** Unrecognized modifiers (e.g., `{1:unknown}`) fall back to leaving the original text intact without throwing errors.

### 3. Escape Sequence Handling
Validates escape sequences within output template strings:
- `\n` $\rightarrow$ Literal newline
- `\t` $\rightarrow$ Literal tab
- `\{` $\rightarrow$ Literal open brace `{`
- `\\` $\rightarrow$ Literal backslash `\`

### 4. `GrabTemplateExecutor.ValidateOutputTemplate`
Validates templates against provided region indexes and pattern names, returning a list of descriptive issue strings:
- Flags templates referencing non-existent/out-of-range region numbers.
- Flags empty template strings.
- Flags templates that contain static text without referencing available regions.
- Flags unknown pattern references (`{p:PatternName:mode}`) or invalid pattern match modes.

### 5. `GrabTemplateExecutor.ApplyPatternPlaceholders`
Validates regular expression pattern replacement within templates using the syntax `{p:PatternName:mode}` or `{p:PatternName:mode:separator_override}`:
- **`first` mode:** Replaces placeholder with the first matched string.
- **`last` mode:** Replaces placeholder with the last matched string.
- **`all` mode:** Joins all regex matches using either a default separator or a custom inline separator (e.g., `{p:Email:all: | }`).
- **Nth Index mode:** Extracts specific match indices (e.g., `{p:Integer:2}` extracts the second match).
- **Multiple Index mode:** Extracts multiple specific matches joined by the configured separator (e.g., `{p:Integer:1,3}`).
- **Edge Cases:**
  - No matches found $\rightarrow$ Replaced with empty string.
  - Pattern not in regex dictionary $\rightarrow$ Replaced with empty string.
  - Pattern name not in registered pattern list $\rightarrow$ Leaves original placeholder unchanged.
  - Requested index out of range $\rightarrow$ Replaced with empty string.

### 6. `GrabTemplateExecutor.ExtractMatchesByMode`
Validates direct match collection extraction given a `MatchCollection` instance and extraction modes (`first`, `last`, `all`).

### 7. Hybrid Execution & Pipeline Integration
Validates the sequential execution pipeline: applying output region substitution first via `ApplyOutputTemplate`, followed by pattern placeholder evaluation via `ApplyPatternPlaceholders`.

### 8. `GrabTemplate` Model Tests
Validates core model logic:
- **`IsValid`:** Asserts validity requiring both `Name` and `OutputTemplate` properties to be set.
- **`GetReferencedPatternNames()`:** Parses output template strings to extract pattern name references.
- **`IsTextOnly`:** Evaluates to `true` when no regions are attached to the template, and `false` when regions are present.

### 9. `GrabTemplateExecutor.ApplyTextOnlyTemplate`
Validates behavior when applying templates directly to text input without explicit region maps:
- Evaluates literal output templates regardless of input text.
- Resolves region placeholders (e.g., `{1}`) to empty strings.
- Evaluates escape sequences.
- Returns input text unchanged when the output template is invalid or empty.

---

## Detailed Test Case Reference

| Test Name | Feature / Method Tested | Target Behavior |
| :--- | :--- | :--- |
| `ApplyOutputTemplate_SingleRegion_SubstitutesCorrectly` | `ApplyOutputTemplate` | Replaces `{1}` with `"Alice"`. |
| `ApplyOutputTemplate_MultipleRegions_SubstitutesAll` | `ApplyOutputTemplate` | Replaces `{1}` and `{2}` in template. |
| `ApplyOutputTemplate_MissingRegion_ReplacesWithEmpty` | `ApplyOutputTemplate` | Missing region `{2}` resolves to an empty string. |
| `ApplyOutputTemplate_EmptyTemplate_ReturnsEmpty` | `ApplyOutputTemplate` | Empty string template returns an empty string. |
| `ApplyOutputTemplate_TrimModifier_TrimsWhitespace` | `{1:trim}` Modifier | Trims surrounding whitespace from value. |
| `ApplyOutputTemplate_UpperModifier_ConvertsToUpper` | `{1:upper}` Modifier | Converts value to uppercase. |
| `ApplyOutputTemplate_LowerModifier_ConvertsToLower` | `{1:lower}` Modifier | Converts value to lowercase. |
| `ApplyOutputTemplate_UnknownModifier_LeavesTextAsIs` | Modifiers | Leaves string unmodified when given an unknown modifier. |
| `ApplyOutputTemplate_NewlineEscape_InsertsNewline` | Escape Sequences | Converts `\n` to a newline character. |
| `ApplyOutputTemplate_TabEscape_InsertsTab` | Escape Sequences | Converts `\t` to a tab character. |
| `ApplyOutputTemplate_LiteralBraceEscape_PreservesBrace` | Escape Sequences | Converts `\{` to `{`. |
| `ApplyOutputTemplate_DoubleBackslash_PreservesBackslash` | Escape Sequences | Converts `\\` to `\`. |
| `ValidateOutputTemplate_ValidTemplate_ReturnsNoIssues` | `ValidateOutputTemplate` | Returns an empty issue list for valid input. |
| `ValidateOutputTemplate_OutOfRangeRegion_ReturnsIssue` | `ValidateOutputTemplate` | Returns issue if template uses unmapped region index. |
| `ValidateOutputTemplate_EmptyTemplate_ReturnsIssue` | `ValidateOutputTemplate` | Returns issue if template string is empty. |
| `ValidateOutputTemplate_NoRegionsReferenced_ReturnsIssue` | `ValidateOutputTemplate` | Returns issue if static template omits available regions. |
| `ApplyPatternPlaceholders_FirstMatch_ReturnsFirstOccurrence` | `ApplyPatternPlaceholders` | Substitutes `{p:Email:first}` with first match. |
| `ApplyPatternPlaceholders_LastMatch_ReturnsLastOccurrence` | `ApplyPatternPlaceholders` | Substitutes `{p:Email:last}` with last match. |
| `ApplyPatternPlaceholders_AllMatches_JoinsWithDefaultSeparator` | `ApplyPatternPlaceholders` | Substitutes `{p:Email:all}` joining with default separator `, `. |
| `ApplyPatternPlaceholders_AllMatchesCustomSeparator_UsesOverride` | `ApplyPatternPlaceholders` | Substitutes `{p:Email:all: \| }` using custom separator ` \| `. |
| `ApplyPatternPlaceholders_NthMatch_ReturnsSingleIndex` | `ApplyPatternPlaceholders` | Substitutes `{p:Integer:2}` with the second match. |
| `ApplyPatternPlaceholders_MultipleIndices_JoinsSelected` | `ApplyPatternPlaceholders` | Substitutes `{p:Integer:1,3}` with 1st and 3rd matches. |
| `ApplyPatternPlaceholders_NoMatches_ReturnsEmpty` | `ApplyPatternPlaceholders` | Replaces placeholder with empty string when regex yields 0 matches. |
| `ApplyPatternPlaceholders_PatternNotFound_ReturnsEmpty` | `ApplyPatternPlaceholders` | Replaces placeholder with empty string when regex dictionary lacks match. |
| `ApplyPatternPlaceholders_UnknownPatternName_LeavesPlaceholder` | `ApplyPatternPlaceholders` | Leaves `{p:Unknown:first}` untouched if pattern is unregistered. |
| `ApplyPatternPlaceholders_IndexOutOfRange_ReturnsEmpty` | `ApplyPatternPlaceholders` | Replaces placeholder with empty string if match index exceeds total matches. |
| `ExtractMatchesByMode_First_ReturnsFirst` | `ExtractMatchesByMode` | Extracts first item from `MatchCollection`. |
| `ExtractMatchesByMode_Last_ReturnsLast` | `ExtractMatchesByMode` | Extracts last item from `MatchCollection`. |
| `ExtractMatchesByMode_All_JoinsAll` | `ExtractMatchesByMode` | Joins all items from `MatchCollection`. |
| `HybridTemplate_RegionsAndPatterns_BothResolved` | Sequential Pipeline | Verifies combined region replacement followed by pattern matching. |
| `GrabTemplate_IsValid_PatternOnlyTemplate` | `GrabTemplate.IsValid` | Evaluates to `true` when `Name` and `OutputTemplate` exist. |
| `GrabTemplate_IsValid_RequiresNameAndOutput` | `GrabTemplate.IsValid` | Evaluates to `false` when `Name` is missing. |
| `GrabTemplate_GetReferencedPatternNames_ParsesNames` | `GrabTemplate` | Extracts pattern names from `{p:Name:mode}` placeholders. |
| `GrabTemplate_IsTextOnly_TrueWhenNoRegions` | `GrabTemplate.IsTextOnly` | Evaluates to `true` if `Regions` list is empty or omitted. |
| `GrabTemplate_IsTextOnly_FalseWhenRegionsPresent` | `GrabTemplate.IsTextOnly` | Evaluates to `false` when `Regions` collection contains regions. |
| `ApplyTextOnlyTemplate_LiteralOutput_IgnoresInputText` | `ApplyTextOnlyTemplate` | Outputs literal template content ignoring original text input. |
| `ApplyTextOnlyTemplate_RegionPlaceholders_ResolveToEmpty` | `ApplyTextOnlyTemplate` | Region placeholders like `{1}` resolve to empty strings. |
| `ApplyTextOnlyTemplate_EscapeSequences_AreProcessed` | `ApplyTextOnlyTemplate` | Escape sequences like `\n` process correctly. |
| `ApplyTextOnlyTemplate_InvalidTemplate_ReturnsInputUnchanged` | `ApplyTextOnlyTemplate` | Returns original input unmodified when output template is invalid/empty. |
| `ValidateOutputTemplate_ValidPatternPlaceholder_NoIssues` | `ValidateOutputTemplate` | Returns no issues for valid pattern placeholders. |
| `ValidateOutputTemplate_UnknownPatternName_ReturnsIssue` | `ValidateOutputTemplate` | Returns issue when pattern placeholder references an unlisted pattern name. |
| `ValidateOutputTemplate_InvalidMatchMode_ReturnsIssue` | `ValidateOutputTemplate` | Returns issue when pattern match mode is unrecognized. |