# Technical Documentation: `Tests/EditTextWindowSpellCheckTests.cs`

## Overview

The `EditTextWindowSpellCheckTests` class is a suite of unit tests in the `Tests` namespace. Its primary objective is to validate the behavior of the `EditTextWindow.ShouldEnableSpellCheck` method in the `Text_Grab` namespace. 

These tests verify the heuristics and explicit mode settings that determine whether spell checking should be automatically enabled or disabled for a given text payload.

---

## Technical Summary

* **Namespace**: `Tests`
* **Target Assembly Reference**: `Text_Grab`
* **Testing Framework**: xUnit (`[Fact]`, `Assert.True`, `Assert.False`)
* **Core Functionality Tested**: 
  * Overloads of `EditTextWindow.ShouldEnableSpellCheck`
  * Text length thresholds
  * Word/token length boundaries
  * Count thresholds for long tokens
  * `SpellCheckMode` enumeration behavior (`Auto`, `AlwaysOn`, `Off`)

---

## Spell Check Logic & Heuristics Under Test

Based on the test assertions, `EditTextWindow.ShouldEnableSpellCheck` evaluates whether to enable spell checking using the following rules:

1. **Text Length Threshold**:
   * Text exceeding 10,000 characters causes `ShouldEnableSpellCheck` to return `false` under auto/heuristic evaluation.
2. **Long Word Length Boundary**:
   * A token/word must be **25 characters or longer** to be classified as a "long word." Tokens of 24 characters or fewer are not counted as long words.
3. **Long Word Count Threshold**:
   * Having **3 or more long words** ($\ge 25$ characters) in the text causes `ShouldEnableSpellCheck` to return `false` under auto/heuristic evaluation.
   * Having **2 or fewer long words** keeps spell checking enabled (`true`).
4. **`SpellCheckMode` Override Settings**:
   * **`SpellCheckMode.Auto`**: Uses the heuristic rules (text length and long word counts) to decide.
   * **`SpellCheckMode.AlwaysOn`**: Forces spell check to be enabled (`true`), ignoring text length and long word counts.
   * **`SpellCheckMode.Off`**: Forces spell check to be disabled (`false`), regardless of content.

---

## Test Methods Summary

| Test Method Name | Validated Condition / Scenario | Expected Outcome |
| :--- | :--- | :--- |
| `NormalSentence_SpellCheckEnabled` | Standard English sentence. | `true` |
| `EmptyString_SpellCheckEnabled` | `string.Empty` input. | `true` |
| `TextExceedsLengthThreshold_SpellCheckDisabled` | String length of 10,001 characters. | `false` |
| `TwoLongWords_SpellCheckEnabled` | Text containing exactly 2 words $\ge 25$ characters. | `true` |
| `ThreeLongWords_SpellCheckDisabled` | Text containing 3 words $\ge 25$ characters. | `false` |
| `AppManifestLikeContent_SpellCheckDisabled` | XML app manifest snippet containing multiple long attributes/strings. | `false` |
| `WordExactlyAtLongWordLength_NotCountedAsLong` | Boundary test: two 24-character words and one 25-character word (only 1 long word counted). | `true` |
| `GuidTokens_SpellCheckDisabled` | Text containing 3 or more unhyphenated GUID/token parameters ($\ge 25$ chars each). | `false` |
| `AlwaysOnMode_EnabledEvenForContentAutoWouldReject` | `SpellCheckMode.AlwaysOn` passed with text containing 3+ long tokens. | `true` (overriding `Auto` mode's `false`) |
| `OffMode_DisabledEvenForNormalText` | `SpellCheckMode.Off` passed with normal sentence text. | `false` (overriding `Auto` mode's `true`) |
| `AutoMode_MatchesContentHeuristic` | `SpellCheckMode.Auto` explicitly passed with normal text vs. oversized text. | `true` for normal, `false` for oversized |

---

## Detailed Test Method Breakdown

### 1. `NormalSentence_SpellCheckEnabled()`
Verifies that standard sentence text ("The quick brown fox jumps over the lazy dog.") returns `true` when tested with the single-parameter overload `ShouldEnableSpellCheck(string)`.

### 2. `EmptyString_SpellCheckEnabled()`
Verifies that an empty string (`string.Empty`) returns `true`.

### 3. `TextExceedsLengthThreshold_SpellCheckDisabled()`
Generates a string of 10,001 characters (`new string('a', 10_001)`). Asserts that `ShouldEnableSpellCheck` returns `false` due to exceeding the maximum length heuristic threshold.

### 4. `TwoLongWords_SpellCheckEnabled()`
Passes a string containing two long tokens exceeding 24 characters (`SomeVeryLongManifestTokenThatIsOver25Chars` and `AnotherReallyLongTokenHere123`). Since the threshold for disabling is 3 long words, this asserts `true`.

### 5. `ThreeLongWords_SpellCheckDisabled()`
Passes a string containing three long tokens $\ge 25$ characters (`Microsoft.Windows.AppManifest.Version1234`, `com.example.application.package.name.v2`, and `SomeGuidLike_1234567890abcdef1234`). Asserts that spell check evaluates to `false`.

### 6. `AppManifestLikeContent_SpellCheckDisabled()`
Provides an XML app manifest string literal containing URIs and manifest attributes. Asserts that complex technical markup containing multiple long tokens evaluates to `false`.

### 7. `WordExactlyAtLongWordLength_NotCountedAsLong()`
Tests boundary behavior for word length definition:
* Constructs two 24-character words and one 25-character word.
* Only the 25-character word is recognized as long.
* Since total long words = 1 (less than 3), `ShouldEnableSpellCheck` returns `true`.

### 8. `GuidTokens_SpellCheckDisabled()`
Tests string inputs resembling parameters with 32+ character GUID or hash values. Because there are 3 tokens exceeding 25 characters, `ShouldEnableSpellCheck` returns `false`.

### 9. `AlwaysOnMode_EnabledEvenForContentAutoWouldReject()`
Tests the `SpellCheckMode` overload: `EditTextWindow.ShouldEnableSpellCheck(SpellCheckMode, string)`.
* Confirms that `SpellCheckMode.Auto` returns `false` for text with 3+ long tokens.
* Confirms that `SpellCheckMode.AlwaysOn` returns `true` for the exact same text payload.

### 10. `OffMode_DisabledEvenForNormalText()`
* Confirms that `SpellCheckMode.Auto` returns `true` for standard text.
* Confirms that `SpellCheckMode.Off` returns `false` for standard text.

### 11. `AutoMode_MatchesContentHeuristic()`
Ensures `SpellCheckMode.Auto` directly mirrors the logic of the heuristic-based checks:
* Returns `true` for standard short text.
* Returns `false` for text exceeding 10,000 characters.