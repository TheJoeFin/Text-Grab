# Technical Documentation: `SettingsStorageExtensions.cs`

## Overview

The `SettingsStorageExtensions` class provides a set of C# extension methods for managing application data, settings, and file storage in Windows desktop/UWP applications. Located in the `Text_Grab.Helpers` namespace, this utility class simplifies reading, writing, and serializing data to `StorageFolder`, `ApplicationDataContainer`, `StorageFile`, and checking `ApplicationData` status.

It relies on JSON serialization routines via a `Json` utility class to convert strongly-typed objects into string data for persistence and vice versa.

---

## File Information

- **File Path:** `Text-Grab/Extensions/SettingsStorageExtensions.cs`
- **Namespace:** `Text_Grab.Helpers`
- **Class Type:** `public static class`

---

## Constants

| Constant | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `FileExtension` | `string` | `".json"` | Default file extension applied to named storage files when saving or loading JSON data. |

---

## Method Documentation

### 1. ApplicationData Extensions

#### `IsRoamingStorageAvailable`
Checks if roaming storage is available for the given `ApplicationData` instance.

```csharp
public static bool IsRoamingStorageAvailable(this ApplicationData appData)
```

- **Parameters:**
  - `appData` (`ApplicationData`): The target application data instance.
- **Returns:** `bool` — Returns `true` if `RoamingStorageQuota` is equal to `0`; otherwise `false`.

---

### 2. StorageFolder Extensions (JSON Operations)

#### `SaveAsync<T>`
Serializes an object of type `T` into JSON format and saves it as a file inside the specified `StorageFolder`.

```csharp
public static async Task SaveAsync<T>(this StorageFolder folder, string name, T content)
```

- **Type Parameters:**
  - `T`: The type of object to serialize.
- **Parameters:**
  - `folder` (`StorageFolder`): The target folder to write the file into.
  - `name` (`string`): The base name of the file (without extension).
  - `content` (`T`): The object to save.
- **Behavior:**
  1. Performs a null check on `content`. If `content` is `null`, execution halts and returns early.
  2. Generates the full file name by appending `.json` via `GetFileName(name)`.
  3. Creates or overwrites the file using `CreationCollisionOption.ReplaceExisting`.
  4. Serializes `content` asynchronously using `Json.StringifyAsync(content)`.
  5. Writes the string to the storage file using `FileIO.WriteTextAsync`.

---

#### `ReadAsync<T>`
Reads a JSON file from the target `StorageFolder` and deserializes its contents into an instance of type `T`.

```csharp
public static async Task<T?> ReadAsync<T>(this StorageFolder folder, string name)
```

- **Type Parameters:**
  - `T`: The expected object type after deserialization.
- **Parameters:**
  - `folder` (`StorageFolder`): The storage folder containing the file.
  - `name` (`string`): The base name of the file (without extension).
- **Returns:** `Task<T?>` — Deserialized object of type `T`, or `default` if the file does not exist.
- **Behavior:**
  1. Checks file existence using `File.Exists(Path.Combine(folder.Path, GetFileName(name)))`.
  2. If the file does not exist, returns `default`.
  3. If present, retrieves the file using `folder.GetFileAsync($"{name}.json")`.
  4. Reads the string content using `FileIO.ReadTextAsync`.
  5. Deserializes the JSON string asynchronously using `Json.ToObjectAsync<T>(fileContent)` and returns the result.

---

### 3. ApplicationDataContainer Extensions (Settings)

#### `SaveAsync<T>`
Serializes an object of type `T` to JSON and stores it as a key-value entry in the `ApplicationDataContainer`.

```csharp
public static async Task SaveAsync<T>(this ApplicationDataContainer settings, string key, T? value)
```

- **Type Parameters:**
  - `T`: The type of the value being saved.
- **Parameters:**
  - `settings` (`ApplicationDataContainer`): The target settings container.
  - `key` (`string`): The key associated with the setting.
  - `value` (`T?`): The value object to serialize and save.
- **Behavior:**
  - Performs a null check on `value`. If `null`, execution halts and returns early.
  - Serializes `value` via `Json.StringifyAsync` and passes the resulting string to `SaveString`.

---

#### `SaveString`
Stores a raw string directly in the `ApplicationDataContainer` under the specified key.

```csharp
public static void SaveString(this ApplicationDataContainer settings, string key, string value)
```

- **Parameters:**
  - `settings` (`ApplicationDataContainer`): The settings container.
  - `key` (`string`): Setting key.
  - `value` (`string`): String value to store.
- **Behavior:**
  - Directly assigns `settings.Values[key] = value`.

---

#### `ReadAsync<T>`
Reads a serialized string setting from `ApplicationDataContainer` by key and deserializes it back to type `T`.

```csharp
public static async Task<T?> ReadAsync<T>(this ApplicationDataContainer settings, string key)
```

- **Type Parameters:**
  - `T`: Target object type.
- **Parameters:**
  - `settings` (`ApplicationDataContainer`): The settings container.
  - `key` (`string`): Setting key to retrieve.
- **Returns:** `Task<T?>` — Deserialized instance of `T`, or `default` if the key is not found.
- **Behavior:**
  1. Attempts to retrieve the object using `settings.Values.TryGetValue(key, out object? obj)`.
  2. If found, casts `obj` to `string` and deserializes it via `Json.ToObjectAsync<T>`.
  3. If key is missing, returns `default`.

---

### 4. StorageFolder & StorageFile Extensions (Binary Operations)

#### `SaveFileAsync`
Saves a raw byte array as a file inside a `StorageFolder`.

```csharp
public static async Task<StorageFile> SaveFileAsync(
    this StorageFolder folder, 
    byte[] content, 
    string fileName, 
    CreationCollisionOption options = CreationCollisionOption.ReplaceExisting)
```

- **Parameters:**
  - `folder` (`StorageFolder`): Target storage folder.
  - `content` (`byte[]`): Raw binary data to write. Cannot be `null`.
  - `fileName` (`string`): Full file name including extension. Cannot be `null` or empty.
  - `options` (`CreationCollisionOption`, optional): Options for handling existing files. Defaults to `CreationCollisionOption.ReplaceExisting`.
- **Returns:** `Task<StorageFile>` — The created `StorageFile` instance.
- **Exceptions Thrown:**
  - `ArgumentNullException`: Thrown if `content` is `null`.
  - `ArgumentException`: Thrown if `fileName` is null or empty.
- **Behavior:**
  1. Validates `content` and `fileName` arguments.
  2. Calls `folder.CreateFileAsync(fileName, options)`.
  3. Writes binary content using `FileIO.WriteBytesAsync(storageFile, content)`.
  4. Returns the created `StorageFile`.

---

#### `ReadFileAsync`
Attempts to locate a file in a `StorageFolder` and read its content as a byte array.

```csharp
public static async Task<byte[]?> ReadFileAsync(this StorageFolder folder, string fileName)
```

- **Parameters:**
  - `folder` (`StorageFolder`): Folder to search.
  - `fileName` (`string`): Name of the file to read.
- **Returns:** `Task<byte[]?>` — Byte array containing file content, or `null` if the item does not exist or is not a file.
- **Behavior:**
  1. Calls `folder.TryGetItemAsync(fileName)`.
  2. Validates that the item is non-null and that `item.IsOfType(StorageItemTypes.File)` evaluates to `true`.
  3. Obtains the `StorageFile` via `folder.GetFileAsync(fileName)`.
  4. Reads and returns bytes using `storageFile.ReadBytesAsync()`.
  5. Returns `null` if the target is invalid or non-existent.

---

#### `ReadBytesAsync`
Reads raw binary bytes directly from a `StorageFile` stream.

```csharp
public static async Task<byte[]?> ReadBytesAsync(this StorageFile file)
```

- **Parameters:**
  - `file` (`StorageFile`): Target file to read.
- **Returns:** `Task<byte[]?>` — Array of bytes containing file content, or `null` if `file` is `null`.
- **Behavior:**
  1. Returns `null` if `file` is `null`.
  2. Opens a random-access stream using `await file.OpenReadAsync()`.
  3. Instantiates a `DataReader` positioned at stream index `0`.
  4. Loads data asynchronously using `await reader.LoadAsync((uint)stream.Size)`.
  5. Allocates a byte array sized to `stream.Size` and reads stream bytes into the buffer via `reader.ReadBytes(bytes)`.
  6. Disposes streams/readers automatically via `using` declarations and returns `bytes`.

---

### 5. Private Helper Methods

#### `GetFileName`
Appends the default `.json` file extension to a provided name string.

```csharp
private static string GetFileName(string name)
```

- **Parameters:**
  - `name` (`string`): Base file name.
- **Returns:** `string` — Combined string (`name` + `".json"`).

---

## Dependencies & Class Relationships

```
SettingsStorageExtensions
  │
  ├── Windows.Storage (StorageFolder, StorageFile, ApplicationDataContainer, ApplicationData, FileIO)
  ├── Windows.Storage.Streams (IRandomAccessStream, DataReader)
  └── Json (External helper class used for Json.StringifyAsync and Json.ToObjectAsync)
```