# Technical Documentation: `WrappingStream` (`StreamWrapper.cs`)

**File Path:** `Text-Grab/Utilities/StreamWrapper.cs`  
**Namespace:** `Text_Grab`  
**Class Name:** `WrappingStream`  
**Base Class:** `System.IO.Stream`  

---

## 1. Overview

The `WrappingStream` class is a wrapper implementation of the abstract `System.IO.Stream` class. Its primary design purpose is to wrap an existing `Stream` instance while preventing the underlying stream (`m_streamBase`) from being closed or disposed when the wrapper itself is disposed.

This pattern is particularly useful when passing streams into classes such as `BinaryReader` or `System.Security.Cryptography.CryptoStream` that automatically dispose of their underlying streams upon their own disposal. By using `WrappingStream`, the underlying stream remains open and accessible after the wrapping consumer is disposed.

---

## 2. Fields

| Field | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `m_streamBase` | `Stream?` | `private` | The reference to the underlying stream being wrapped. Set to `null` upon disposal. |

---

## 3. Class Constructor

### `WrappingStream(Stream streamBase)`

Initializes a new instance of the `WrappingStream` class.

* **Parameters:**
  * `streamBase` (`Stream`): The base stream to wrap.
* **Exceptions:**
  * `ArgumentNullException`: Thrown if `streamBase` is `null`.

---

## 4. Properties

### Overridden Stream Properties

| Property | Type | Access | Read/Write | Description |
| :--- | :--- | :--- | :--- | :--- |
| `CanRead` | `bool` | `public` | Read-only | Returns `true` if `m_streamBase` is not `null` and supports reading; otherwise, `false`. |
| `CanSeek` | `bool` | `public` | Read-only | Returns `true` if `m_streamBase` is not `null` and supports seeking; otherwise, `false`. |
| `CanWrite` | `bool` | `public` | Read-only | Returns `true` if `m_streamBase` is not `null` and supports writing; otherwise, `false`. |
| `Length` | `long` | `public` | Read-only | Throws `ObjectDisposedException` if disposed. Returns `m_streamBase.Length` if valid, otherwise `0`. |
| `Position` | `long` | `public` | Read/Write | Throws `ObjectDisposedException` if disposed. **Get:** Returns `m_streamBase.Position` (or `0`). **Set:** Sets `m_streamBase.Position`. |

### Protected Properties

| Property | Type | Access | Read/Write | Description |
| :--- | :--- | :--- | :--- | :--- |
| `WrappedStream` | `Stream?` | `protected` | Read-only | Provides derived classes access to the internal wrapped stream instance (`m_streamBase`). |

---

## 5. Methods

### Synchronous I/O Operations

#### `Read(byte[] buffer, int offset, int count)`
* **Returns:** `int` – The total number of bytes read into the buffer.
* **Behavior:** Throws `ObjectDisposedException` if disposed. Delegates to `m_streamBase.Read(...)` if not `null`, otherwise returns `0`.

#### `ReadByte()`
* **Returns:** `int` – The byte cast to an integer, or `-1` (delegated) / `0` if empty or `m_streamBase` is `null`.
* **Behavior:** Throws `ObjectDisposedException` if disposed. Delegates to `m_streamBase.ReadByte()`.

#### `Write(byte[] buffer, int offset, int count)`
* **Returns:** `void`
* **Behavior:** Throws `ObjectDisposedException` if disposed. Forwards the operation to `m_streamBase.Write(...)`.

#### `WriteByte(byte value)`
* **Returns:** `void`
* **Behavior:** Throws `ObjectDisposedException` if disposed. Forwards the byte to `m_streamBase.WriteByte(...)`.

#### `Flush()`
* **Returns:** `void`
* **Behavior:** Throws `ObjectDisposedException` if disposed. Clears buffers by calling `m_streamBase.Flush()`.

#### `Seek(long offset, SeekOrigin origin)`
* **Returns:** `long` – The new position within the stream.
* **Behavior:** Throws `ObjectDisposedException` if disposed. Delegates to `m_streamBase.Seek(...)` or returns `0`.

#### `SetLength(long value)`
* **Returns:** `void`
* **Behavior:** Throws `ObjectDisposedException` if disposed. Sets length on `m_streamBase.SetLength(...)`.

---

### Asynchronous I/O Operations

#### `BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)`
* **Returns:** `IAsyncResult`
* **Behavior:** Throws `ObjectDisposedException` if disposed. If `m_streamBase`, `callback`, and `state` are all non-null, delegates to `m_streamBase.BeginRead`. Otherwise, returns a new `NullAsyncResult()`.

#### `EndRead(IAsyncResult asyncResult)`
* **Returns:** `int` – The number of bytes read.
* **Behavior:** Throws `ObjectDisposedException` if disposed. Delegates to `m_streamBase.EndRead(asyncResult)` if `m_streamBase` is not null; otherwise returns `0`.

#### `BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)`
* **Returns:** `IAsyncResult`
* **Behavior:** Throws `ObjectDisposedException` if disposed. If `m_streamBase`, `callback`, and `state` are all non-null, delegates to `m_streamBase.BeginWrite`. Otherwise, returns a new `NullAsyncResult()`.

#### `EndWrite(IAsyncResult asyncResult)`
* **Returns:** `void`
* **Behavior:** Throws `ObjectDisposedException` if disposed. Calls `m_streamBase?.EndWrite(asyncResult)`.

---

### Lifecycle & Guard Methods

#### `Dispose(bool disposing)`
* **Access:** `protected override`
* **Parameters:** `disposing` (`bool`) – `true` to release both managed and unmanaged resources; `false` to release only unmanaged resources.
* **Behavior:** Detaches the base stream by setting `m_streamBase = null` without calling `.Dispose()` or `.Close()` on the underlying base stream. Finally calls `base.Dispose(disposing)`.

#### `ThrowIfDisposed()`
* **Access:** `private`
* **Returns:** `void`
* **Behavior:** Helper validation method. Checks if `m_streamBase == null`. If `true`, throws an `ObjectDisposedException` initialized with the current type name (`WrappingStream`).

---

## 6. How It Works: Disposal Mechanics

 standard stream wrappers dispose of their inner stream when `Dispose()` is invoked. `WrappingStream` overrides this lifecycle behavior:

```
[ Call Dispose() on WrappingStream ]
                 │
                 ▼
     Dispose(bool disposing)
                 │
                 ├── Set m_streamBase = null  <-- Detaches reference (Base stream remains OPEN)
                 │
                 ▼
       base.Dispose(disposing)
```

1. **Active State:** While active, calls to reading, writing, seeking, and properties are delegated directly to `m_streamBase`.
2. **Disposal:** Calling `Dispose()` resets `m_streamBase` to `null`.
3. **Disposed State:** Subsequent method or property calls (except state checks like `CanRead`, `CanWrite`, `CanSeek`) hit `ThrowIfDisposed()`, which throws an `ObjectDisposedException`.
4. **Underlying Stream Intact:** Because `m_streamBase.Dispose()` was never called, the wrapped underlying stream remains open for further usage elsewhere in the application.