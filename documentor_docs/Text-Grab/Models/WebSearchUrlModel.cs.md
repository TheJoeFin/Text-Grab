# Technical Documentation: `WebSearchUrlModel.cs`

## Overview

The `WebSearchUrlModel` record defined in `WebSearchUrlModel.cs` represents a web search engine configuration within the Text-Grab application. It serves a dual purpose:
1. Act as a data model containing the `Name` and search query template `Url` for individual search engines.
2. Provide properties and helper methods to retrieve, manage, and persist web search configurations and default search engine settings using application utility services.

---

## Namespace & Dependencies

- **Namespace**: `Text_Grab.Models`
- **Dependencies**:
  - `System.Collections.Generic`
  - `System.Linq`
  - `Text_Grab.Utilities` (uses `AppUtilities`)

---

## Class Definition

```csharp
public record WebSearchUrlModel
```

`WebSearchUrlModel` is defined as a C# `record`.

---

## Properties

### 1. `Name`
- **Type**: `string`
- **Access**: `public { get; set; }`
- **Default**: `string.Empty`
- **Description**: Stores the display name of the search engine (e.g., `"Google"`, `"GitHub Code"`).

### 2. `Url`
- **Type**: `string`
- **Access**: `public { get; set; }`
- **Default**: `string.Empty`
- **Description**: Stores the base search URL with query parameter formatting (e.g., `"https://www.google.com/search?q="`).

### 3. `DefaultSearcher`
- **Type**: `WebSearchUrlModel`
- **Access**: `public { get; set; }`
- **Description**: Manages the current default web search provider.
  - **Getter**: Lazily initializes `defaultSearcher` by calling `GetDefaultSearcher()` if `defaultSearcher` is `null`, then returns it.
  - **Setter**: Sets the `defaultSearcher` field and persists the change to application settings via `SaveDefaultSearcher(value)`.

### 4. `WebSearchers`
- **Type**: `List<WebSearchUrlModel>`
- **Access**: `public { get; set; }`
- **Description**: Manages the collection of available web search providers.
  - **Getter**: Checks if the internal list `webSearchers` is empty (`Count == 0`). If empty, populates it by calling `GetWebSearchUrls()`, then returns the list.
  - **Setter**: Updates the internal list `webSearchers` and persists the updated list using `SaveWebSearchUrls(value)`.

---

## Methods

### Instance Methods

#### `ToString()`
```csharp
public override string ToString() => Name;
```
- **Return Type**: `string`
- **Description**: Overrides the standard `ToString()` method to return the `Name` property.

#### `GetDefaultSearcher()`
```csharp
private WebSearchUrlModel GetDefaultSearcher()
```
- **Return Type**: `WebSearchUrlModel`
- **Description**:
  1. Retrieves the setting value stored in `AppUtilities.TextGrabSettings.DefaultWebSearch`.
  2. If the setting string is null, empty, or whitespace, returns the first item in `WebSearchers` (`WebSearchers[0]`).
  3. Otherwise, searches `WebSearchers` for an entry whose `Name` matches the setting value using LINQ `.FirstOrDefault()`.
  4. Returns the matching provider, or falls back to `WebSearchers[0]` if no match is found.

#### `SaveDefaultSearcher(WebSearchUrlModel webSearchUrl)`
```csharp
private void SaveDefaultSearcher(WebSearchUrlModel webSearchUrl)
```
- **Parameters**: `WebSearchUrlModel webSearchUrl`
- **Return Type**: `void`
- **Description**: Updates `AppUtilities.TextGrabSettings.DefaultWebSearch` with the `Name` of the specified `webSearchUrl` and calls `AppUtilities.TextGrabSettings.Save()` to persist the setting.

---

### Static Methods

#### `GetDefaultWebSearchUrls()`
```csharp
private static List<WebSearchUrlModel> GetDefaultWebSearchUrls()
```
- **Return Type**: `List<WebSearchUrlModel>`
- **Description**: Constructs and returns a hardcoded list of standard default search engines.

**Hardcoded Defaults**:
| Name | URL Template |
| :--- | :--- |
| **Google** | `https://www.google.com/search?q=` |
| **Bing** | `https://www.bing.com/search?q=` |
| **DuckDuckGo** | `https://duckduckgo.com/?q=` |
| **Brave** | `https://search.brave.com/search?q=` |
| **GitHub Code** | `https://github.com/search?type=code&q=` |
| **GitHub Repos** | `https://github.com/search?type=repositories&q=` |

#### `GetWebSearchUrls()`
```csharp
public static List<WebSearchUrlModel> GetWebSearchUrls()
```
- **Return Type**: `List<WebSearchUrlModel>`
- **Description**:
  1. Calls `AppUtilities.TextGrabSettingsService.LoadWebSearchUrls()` to load saved search engines.
  2. If the loaded list is empty (`Count == 0`), calls and returns `GetDefaultWebSearchUrls()`.
  3. Otherwise, returns the loaded list.

#### `SaveWebSearchUrls(List<WebSearchUrlModel> webSearchUrls)`
```csharp
public static void SaveWebSearchUrls(List<WebSearchUrlModel> webSearchUrls)
```
- **Parameters**: `List<WebSearchUrlModel> webSearchUrls`
- **Return Type**: `void`
- **Description**: Pass-through static method that calls `AppUtilities.TextGrabSettingsService.SaveWebSearchUrls(webSearchUrls)` to write the list of search engines to settings storage.

---

## Persistence Workflow Summary

1. **Loading Providers**: `GetWebSearchUrls()` attempts to load saved provider configurations via `AppUtilities.TextGrabSettingsService`. If none are saved, it falls back to a hardcoded set of 6 default providers.
2. **Saving Providers**: Calling `SaveWebSearchUrls(...)` or assigning a new list to `WebSearchers` triggers `AppUtilities.TextGrabSettingsService.SaveWebSearchUrls(...)`.
3. **Retrieving Default Provider**: `GetDefaultSearcher()` reads `AppUtilities.TextGrabSettings.DefaultWebSearch` by name and matches it against `WebSearchers`. Defaults to `WebSearchers[0]` if blank or not matched.
4. **Setting Default Provider**: Assigning a new value to `DefaultSearcher` updates `AppUtilities.TextGrabSettings.DefaultWebSearch` and invokes `AppUtilities.TextGrabSettings.Save()`.