# Technical Documentation: `ThirdPartyPackageInfo.cs`

**File Path:** `Text-Grab/Models/ThirdPartyPackageInfo.cs`  
**Namespace:** `Text_Grab.Models`  

---

## Overview

The `ThirdPartyPackageInfo` file defines a `sealed record` model used to store metadata regarding third-party packages or software dependencies included in the Text-Grab project. It encapsulates package attributes such as identification, versioning, licensing details, project URLs, notice locations, and notes.

---

## Type Definition

```csharp
public sealed record ThirdPartyPackageInfo(
    string PackageId,
    string Version,
    string Scope,
    string License,
    string ProjectUrl,
    string NoticeTarget,
    bool NoticeIsLocal = false,
    string Notes = "")
```

### Characteristics
* **Record Type (`record`):** Provides value-based equality semantics and immutability out of the box.
* **Sealed (`sealed`):** Prevents other classes or records from inheriting from `ThirdPartyPackageInfo`.

---

## Positional Parameters / Properties

The positional parameters defined in the primary constructor automatically map to public positional properties on the record:

| Parameter / Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `PackageId` | `string` | *None (Required)* | The identifier or name of the third-party package. |
| `Version` | `string` | *None (Required)* | The version number or string of the third-party package. |
| `Scope` | `string` | *None (Required)* | The scope or context in which the package is used. |
| `License` | `string` | *None (Required)* | The license type or description governing the package (e.g., MIT, Apache-2.0). |
| `ProjectUrl` | `string` | *None (Required)* | The web URL directing to the source repository or project page. |
| `NoticeTarget` | `string` | *None (Required)* | The file path or web URL pointing to the package's notice or license file. |
| `NoticeIsLocal` | `bool` | `false` | A boolean flag indicating whether `NoticeTarget` refers to a local file (`true`) or an external resource (`false`). |
| `Notes` | `string` | `""` (Empty string) | Optional additional comments or notes regarding the package. |

---

## Computed Properties

### `DisplayNotes`

```csharp
public string DisplayNotes => string.IsNullOrWhiteSpace(Notes) ? "\u2014" : Notes;
```

* **Type:** `string` (Read-only get expression)
* **Behavior:** Checks the `Notes` property using `string.IsNullOrWhiteSpace(Notes)`:
  * Returns an em-dash character (`\u2014` / `—`) if `Notes` is `null`, empty, or consists only of whitespace characters.
  * Returns the value of `Notes` if it contains non-whitespace content.

---

## Usage Example

```csharp
using Text_Grab.Models;

// Instantiate using required and default parameters
var package = new ThirdPartyPackageInfo(
    PackageId: "Example.Library",
    Version: "1.0.0",
    Scope: "Runtime",
    License: "MIT",
    ProjectUrl: "https://example.com/project",
    NoticeTarget: "https://example.com/project/LICENSE"
);

// Accessing properties
string id = package.PackageId;            // "Example.Library"
bool isLocal = package.NoticeIsLocal;      // false
string notesDisplay = package.DisplayNotes; // "—" (Em-dash, since Notes is empty)
```