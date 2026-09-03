# Technical Documentation: `Text-Grab/Utilities/Json.cs`

## Overview

The `Json` static class in the `Text_Grab.Helpers` namespace provides asynchronous wrapper methods for JSON serialization and deserialization using .NET's native `System.Text.Json.JsonSerializer`. 

By executing synchronous `JsonSerializer` operations inside `Task.Run()`, this utility allows CPU-bound JSON processing to be offloaded to a background thread to avoid blocking the calling thread (e.g., UI thread).

---

## Code Details

* **File Path:** `Text-Grab/Utilities/Json.cs`
* **Namespace:** `Text_Grab.Helpers`
* **Class Declaration:** `public static class Json`
* **Dependencies:**
  * `System.Text.Json`
  * `System.Threading.Tasks`

---

## Methods

### 1. `ToObjectAsync<T>(string value)`

Deserializes a JSON string payload into a strongly typed object of type `T` asynchronously.

#### Signature
```csharp
public static async Task<T?> ToObjectAsync<T>(string value)
```

#### Generic Parameters
* **`T`**: The target type to deserialize the JSON string into. Can be nullable.

#### Parameters
* **`value`** (`string`): The JSON string representation to be deserialized.

#### Returns
* **`Task<T?>`**: A task representing the asynchronous operation. The task result contains the deserialized object of type `T`, or `null` if deserialization yields null.

#### Behavior
* Executes `JsonSerializer.Deserialize<T>(value)` asynchronously on a background thread via `Task.Run`.

---

### 2. `StringifyAsync(object value)`

Serializes an object instance into a JSON formatted string asynchronously.

#### Signature
```csharp
public static async Task<string> StringifyAsync(object value)
```

#### Parameters
* **`value`** (`object`): The object to serialize into a JSON string.

#### Returns
* **`Task<string>`**: A task representing the asynchronous operation. The task result contains the serialized JSON string representation of the input object.

#### Behavior
* Executes `JsonSerializer.Serialize(value)` asynchronously on a background thread via `Task.Run`.

---

## How It Works

1. **Background Execution**: Both `ToObjectAsync` and `StringifyAsync` use `Task.Run()` to dispatch synchronous calls to `JsonSerializer.Deserialize<T>` and `JsonSerializer.Serialize` to the .NET ThreadPool.
2. **Asynchronous Await**: The methods use C# `async`/`await` keywords to return the processed results once the background thread completes execution.