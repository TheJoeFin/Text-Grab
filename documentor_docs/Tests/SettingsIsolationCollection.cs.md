# Technical Documentation: `Tests/SettingsIsolationCollection.cs`

## Overview

The `SettingsIsolationCollection.cs` file defines a custom test collection for an xUnit testing framework inside the `Tests` namespace. Its primary purpose is to group specific tests under a named collection called `"Settings isolation"` and explicitly disable parallel execution for all tests belonging to this collection.

---

## File Details

* **File Path:** `Tests/SettingsIsolationCollection.cs`
* **Namespace:** `Tests`
* **Target Framework:** xUnit (indicated by the `CollectionDefinition` attribute)

---

## Code Breakdown

```csharp
namespace Tests;

[CollectionDefinition("Settings isolation", DisableParallelization = true)]
public class SettingsIsolationCollectionDefinition
{
}
```

### Components

#### 1. Namespace: `Tests`
Declares that this collection definition resides within the root `Tests` namespace.

#### 2. Class: `SettingsIsolationCollectionDefinition`
* **Type:** `public class`
* **Purpose:** Acts as a marker class required by xUnit to attach the `CollectionDefinition` attribute. It contains no implementation or logic within its body (`{ }`).

#### 3. Attribute: `[CollectionDefinition(...)]`
This attribute configures the test collection behavior in xUnit. It takes two parameters:

* **Collection Name (`"Settings isolation"`):**
  A string identifier used to associate test classes with this specific collection.
* **`DisableParallelization = true`:**
  A boolean property that forces xUnit to run all test classes within this collection sequentially (one after another) rather than concurrently in parallel.

---

## How It Works

1. **Definition:** The `SettingsIsolationCollectionDefinition` class establishes the existence of the `"Settings isolation"` test collection and sets its execution rules (`DisableParallelization = true`).
2. **Execution Control:** By disabling parallelization, xUnit prevents any tests assigned to this collection from executing simultaneously with each other. This guarantees isolation during test runs, which is typically used when tests interact with shared resources or mutable state (such as application settings).