# ExtractedPatternTests Technical Documentation

## Overview

The `ExtractedPatternTests.cs` file contains the unit test suite for verifying the behavior of the `ExtractedPattern` class (located in `Text_Grab.Models`) and associated helper methods like `Text_Grab.Utilities.StringMethods.ExtractSimplePattern`. 

Written using the xUnit testing framework, these tests validate:
- Constructor initialization and default property values.
- Pattern generation across six distinct precision levels (0 through 5).
- Level labels, level descriptions, and out-of-range fallback logic.
- Automatic heuristic level selection via `DetermineStartingLevel`.
- Case-sensitivity and case-insensitivity controls (`(?i)` regex flags).
- Actual regular expression matching count hierarchies across test samples.

---

## Tested System Summary

Based on the test assertions, the key constants and precision levels supported by `ExtractedPattern` are:

### Precision Levels

| Level | Label (`GetLevelLabel`) | Pattern Concept / Example Input: `"Abc123"` |
| :---: | :--- | :--- |
| **0** | Any Text | Non-whitespace sequences (`\S+`) |
| **1** | Words | Word character sequences (`\w+`) |
| **2** | Length | Fixed length constraints (`\w{3}\w{3}`) |
| **3** | Types | Character type groups with counts (`[A-Za-z]{3}\d{3}`) |
| **4** | Per Char | Literal per-character representation / Case-insensitive (`(?i)Abc123`) |
| **5** | Exact | Exact literal string match (`Abc123`) |

### Constants
- **`MinPrecisionLevel`**: `0`
- **`MaxPrecisionLevel`**: `5`
- **`DefaultPrecisionLevel`**: `3`

---

## Test Categories and Method Descriptions

### 1. Constructor and State Tests

These tests verify object initialization, default properties, and basic collection structure.

*   **`Constructor_GeneratesAllPrecisionLevels()`**
    *   **Goal**: Verifies that constructing an `ExtractedPattern` instance generates a pattern dictionary containing 6 entries (levels 0–5) and sets `IgnoreCase` to `false` by default.
*   **`Constructor_WithIgnoreCase_GeneratesAllPrecisionLevels()`**
    *   **Goal**: Confirms that explicitly passing `ignoreCase: true` sets the `IgnoreCase` property to `true` and generates all 6 precision levels.
*   **`AllPatterns_ContainsAllSixLevels()`**
    *   **Goal**: Verifies that `ExtractedPattern.AllPatterns` contains keys for integer levels 0 through 5.
*   **`AllPatterns_IsReadOnly()`**
    *   **Goal**: Asserts that `ExtractedPattern.AllPatterns` implements `IReadOnlyDictionary<int, string>`.
*   **`Constants_HaveCorrectValues()`**
    *   **Goal**: Asserts exact integer values for `MinPrecisionLevel` (0), `MaxPrecisionLevel` (5), and `DefaultPrecisionLevel` (3).

---

### 2. Pattern Retrieval Tests

These tests check pattern extraction for valid levels, handling of out-of-bounds indices, and consistency.

*   **`GetPattern_ReturnsCorrectPatternForEachLevel(string input, int level, string expectedPattern)`**
    *   **Goal**: Validates generated pattern output for `"Abc123"` across levels 0 to 5.
*   **`GetPattern_WithInvalidLevel_ReturnsDefaultLevel(int invalidLevel)`**
    *   **Goal**: Tests that requesting out-of-range precision levels (e.g., `-1`, `6`, `10`) gracefully falls back to returning the pattern at `DefaultPrecisionLevel` (level 3).
*   **`GetPattern_CalledMultipleTimes_ReturnsSamePattern()`**
    *   **Goal**: Ensures pre-generated pattern strings are idempotent and consistently returned upon repeated calls.
*   **`EmptyString_GeneratesValidPatterns()`**
    *   **Goal**: Ensures that instantiating `ExtractedPattern` with an empty string (`""`) does not throw exceptions and returns non-null patterns for all precision levels.
*   **`ComplexPatterns_GeneratedCorrectly(string input, int level, string expectedPattern)`**
    *   **Goal**: Tests correct pattern generation for complex strings, including phone numbers, spaces, repeating character sets, and special characters needing escaping.

---

### 3. Metadata and Description Tests

Tests covering user-facing string labels and descriptions associated with precision levels.

*   **`GetLevelLabel_ReturnsCorrectLabel(int level, string expectedLabel)`**
    *   **Goal**: Verifies string labels returned by `ExtractedPattern.GetLevelLabel(level)` (e.g., Level 0 = "Any Text", Level 5 = "Exact").
*   **`GetLevelDescription_ReturnsDescriptionForAllLevels()`**
    *   **Goal**: Ensures all valid precision levels (0 through 5) return non-empty, non-whitespace descriptions that do not contain the term `"Unknown"`.
*   **`GetLevelDescription_WithInvalidLevel_ReturnsUnknownMessage()`**
    *   **Goal**: Asserts that passing an invalid level (e.g., `99`) to `GetLevelDescription` contains `"Unknown"`.

---

### 4. Automatic Level Determination (`DetermineStartingLevel`)

This suite tests the static method `ExtractedPattern.DetermineStartingLevel(input)`, which calculates an optimal default precision level depending on input length, character types, delimiters, and formatting.

*   **`DetermineStartingLevel_ShortText_ReturnsHighPrecision`**:
    *   Single character inputs (e.g., `"a"`) return Level 5 (Exact).
    *   2–4 character inputs (e.g., `"AB"`, `"xyz"`, `"test"`) return Level 4 (Per Char).
*   **`DetermineStartingLevel_LongText_ReturnsLowerPrecision`**:
    *   Text exceeding 25 characters returns Level 2 (Length).
*   **`DetermineStartingLevel_PureNumbers_ReturnsLengthFlexible`**:
    *   Purely numeric inputs (e.g., `"123"`, `"4567"`, `"12345"`) return Level 2.
*   **`DetermineStartingLevel_AlphanumericWithDelimiters_ReturnsSeparatorAgnostic`**:
    *   Alphanumeric strings with delimiters (e.g., `"ABC-123"`, `"user_456"`, `"file.txt"`) return Level 3.
*   **`DetermineStartingLevel_MultipleWords_ReturnsStructureOnly`**:
    *   Inputs containing 3 or 4 space-separated words return Level 1.
*   **`DetermineStartingLevel_AlphanumericMixed_ReturnsCharacterClass`**:
    *   Mixed alphanumeric strings without spaces (e.g., `"user123"`, `"AB12CD"`) return Level 3.
*   **`DetermineStartingLevel_SimpleWord_ReturnsCaseInsensitive`**:
    *   Simple standard words (e.g., `"Hello"`, `"Testing"`) return Level 4.
*   **`DetermineStartingLevel_SpecialCharsShort_ReturnsSeparatorAgnostic`**:
    *   Short strings with special characters or multiple delimiters evaluate according to character count and delimiter rules (returning Level 3 or 4).
*   **`DetermineStartingLevel_EmptyOrWhitespace_ReturnsDefault`**:
    *   `null`, empty (`""`), or whitespace strings fall back to returning Level 3.
*   **`DetermineStartingLevel_RepeatingPatterns_ReturnsCharacterClass`**:
    *   Repeating structural patterns with separators (e.g., `"123-456-7890"`, `"XX-YY-ZZ"`) return Level 3.
*   **`DetermineStartingLevel_RealWorldExamples_ProduceSensibleDefaults`**:
    *   Comprehensive dictionary test validating diverse real-world samples (IDs, codes, numbers, words, hashtags, and long text).
*   **`DetermineStartingLevel_EdgeCases_HandledGracefully`**:
    *   Verifies trimming behavior (e.g., `" a "` trims to 1 char and returns Level 5) and whitespace handling (returns Level 3).

---

### 5. Regex Matching & Hierarchy Validation

These integration-style tests execute regex matches against sample text blocks to verify that higher precision levels act restrictively relative to lower levels.

*   **`PrecisionLevels_MatchCountDecreases_FromLevel0ToLevel5`**
    *   **Goal**: Runs generated patterns for `"test"` against a large text sample using `Regex.Matches`. Validates that precision higher levels generate equal or fewer matches than lower levels (e.g., Level 2 matches $\ge$ Level 3 matches, and Level 4 matches $\ge$ Level 5 matches).
*   **`PrecisionLevels_SpecificPattern_MatchCountValidation`**
    *   **Goal**: Evaluates `"ABC123"` patterns against structured text, confirming match count reduction and strict alignment between character class (Level 4) and literal exact (Level 5) patterns under case-insensitive evaluation.
*   **`PrecisionLevels_DemonstrateHierarchy_WithSimpleText`**
    *   **Goal**: Evaluates precise match counts across a controlled text line (`"test Test TEST teST test123 testing best rest"`), verifying exact expectations for each precision level.

---

### 6. Case Sensitivity & Utility Tests (`ExtractSimplePattern`)

These tests target both `ExtractedPattern` with `ignoreCase: true` and the utility method `Text_Grab.Utilities.StringMethods.ExtractSimplePattern`.

*   **`ExtractSimplePattern_WithCaseSensitivity_IncludesCorrectFlag`**: Checks if `(?i)` flag is prepended when `ignoreCase` is `true`, and absent when `false`.
*   **`ExtractSimplePattern_CaseInsensitive_MatchesDifferentCases`**: Ensures `(?i)` flag enables matching across case variations (`test`, `TEST`, `TeSt`).
*   **`ExtractSimplePattern_CaseSensitive_MatchesExactCase`**: Ensures lack of `(?i)` flag restricts matches strictly to exact letter cases.
*   **`ExtractSimplePattern_AllLevels_SupportCaseInsensitiveFlag`**: Verifies that levels 0 through 4 properly prepend `(?i)` when `ignoreCase` is `true`.
*   **`ExtractSimplePattern_DefaultCaseSensitivity_IsFalse`**: Asserts that omitting the `ignoreCase` parameter defaults to `false` (case-sensitive).
*   **`ExtractSimplePattern_CaseInsensitive_CrossPlatformCompatible`**: Verifies inline `(?i)` flag operates independently without requiring external `RegexOptions.IgnoreCase` flags.
*   **`ExtractSimplePattern_CaseFlag_AffectsMatchBehavior`**: Confirms regex instances built from patterns with `(?i)` successfully match varying text cases.
*   **`ExtractSimplePattern_ComplexPattern_WithCaseInsensitivity`**: Verifies multi-word input patterns with special characters retain case-insensitivity.
*   **`ExtractedPattern_WithIgnoreCase_AllPatternsHaveFlag`**: Ensures that when an `ExtractedPattern` instance is created with `ignoreCase: true`, generated patterns for levels 0 through 4 start with `(?i)`.

---

## Dependencies

- **Framework**: .NET / C#
- **Testing Framework**: xUnit (`Xunit.Fact`, `Xunit.Theory`, `Xunit.InlineData`, `Xunit.Assert`)
- **Namespaces Referenced**:
  - `System.Text.RegularExpressions`
  - `Text_Grab.Models`
  - `Text_Grab.Utilities`