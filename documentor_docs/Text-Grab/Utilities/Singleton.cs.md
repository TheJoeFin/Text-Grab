# Developer Documentation: `Text-Grab/Utilities/Singleton.cs`

## Overview

The `Text-Grab/Utilities/Singleton.cs` file defines a generic, thread-safe static class designed to manage single instances of specified classes within the `Text_Grab.Utilities` namespace. It provides a centralized, lazily-initialized pattern for retrieving a single shared instance of any class that has a public parameterless constructor.

---

## File Identification

- **File Path:** `Text-Grab/Utilities/Singleton.cs`
- **Namespace:** `Text_Grab.Utilities`

---

## Class Definition

```csharp
public static class Singleton<T> where T : new()
```

### Type Parameters & Constraints
- **`T`**: The type of class to be instantiated and managed as a singleton.
- **`where T : new()`**: A generic constraint requiring that type `T` must have a public parameterless constructor. This enables the class to instantiate `new T()` dynamically.

---

## Key Components

### 1. `_instances` Field

```csharp
private static ConcurrentDictionary<Type, T> _instances = new();
```

* **Type:** `System.Collections.Concurrent.ConcurrentDictionary<Type, T>`
* **Access Modifier:** `private static`
* **Description:** A thread-safe dictionary that stores the single instance created for a given type `Type`. Because it uses `ConcurrentDictionary`, safe concurrent access across multiple threads is handled automatically without explicit lock statements.

---

### 2. `Instance` Property

```csharp
public static T Instance => _instances.GetOrAdd(typeof(T), (t) => new T());
```

* **Type:** `T`
* **Access Modifier:** `public static`
* **Description:** A expression-bodied static property that provides access to the single instance of type `T`.

---

## How It Works

1. **Access Request:** When `Singleton<T>.Instance` is accessed for a given type `T`, the property evaluates `_instances.GetOrAdd(...)`.
2. **Key Lookup:** It passes `typeof(T)` as the key to look up in the `_instances` dictionary.
3. **Instance Retrieval / Creation:**
   * **If `typeof(T)` is already present in `_instances`:** The existing instance of `T` is returned.
   * **If `typeof(T)` is not present:** The factory delegate `(t) => new T()` is executed, invoking the parameterless constructor of `T` to create a new instance. The new instance is stored in `_instances` under the `typeof(T)` key and then returned.