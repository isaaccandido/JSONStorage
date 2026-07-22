# **JSONStorage**

A simple, dependency-light key/value file storage library. Give it a string key and any object, and it serialises the object with BSON (binary JSON) and writes it to a `.j2k` file named after the key — no schema, no database setup, no manual (de)serialisation code.

The NuGet package ID is `Isaac.FileStorage`, but the code lives under the `Isaac.FileStorage.Lib` namespace (`using Isaac.FileStorage.Lib;`).

## **Quick start**

```csharp
using Isaac.FileStorage.Lib;

var store = new FileStorageEngine("data"); // creates the directory if it doesn't exist

store.Insert("user-42", new { Name = "Ada", Role = "Admin" });

var user = store.Get<dynamic>("user-42");

foreach (var key in store.GetAllKeys())
    Console.WriteLine(key);

store.Delete("user-42");
```

## **Functionalities**

1. *`FileStorageEngine(string dirPath)`*: Opens (or creates) the storage directory at `dirPath`. The resolved full path is exposed via `DirectoryPath`.
2. *`void Insert<T>(string key, T obj)`*: Serialises `obj` and writes it to a file named after `key`. Inserting under an existing key overwrites it.
3. *`T Get<T>(string key)`*: Reads the file named after `key` and deserialises it into `T`.
4. *`IEnumerable<string> GetAllKeys()`*: Returns every key currently stored (every `.j2k` file's base name) in the storage directory.
5. *`void Delete(string key)`*: Deletes the entry for `key`.

## **Exceptions**

All custom exceptions live in `Isaac.FileStorage.Lib.CustomExceptions`.

- **`EmptyKeyException`** — thrown by `Insert`, `Get`, and `Delete` when `key` is `null` or empty.
- **`InvalidKeyException`** — thrown by `Insert`, `Get`, and `Delete` when `key` would resolve to a path outside the storage directory (e.g. `..` traversal or an absolute path).
- **`StorageKeyNotFoundException`** — thrown by `Delete` when `key` doesn't exist.
- **`InvalidOperationException`** — thrown by `Insert`, `Get`, and `Delete` for unexpected I/O or (de)serialisation failures (corrupt data, type mismatch, a file locked by another process, etc). The original exception is preserved as `InnerException`.

The constructor itself throws `ArgumentNullException`/`ArgumentException` if `dirPath` is `null` or empty.

---

## **IMPORTANT NOTE!**

From version 0.3 and above, this solution is BSON-based (binary JSON) rather than plain JSON. Faster to serialise/deserialise, and smaller on disk. Files are saved as `.j2k` (it used to be *a* joke, now it's two).

**Version 2.0 is a breaking release:**

- The root namespace moved from `Isaac.FileStorage` to `Isaac.FileStorage.Lib`.
- The `KeyNotFoundException` custom exception was renamed to `StorageKeyNotFoundException`, to avoid colliding with `System.Collections.Generic.KeyNotFoundException`.
- A path-traversal issue in key handling was fixed — keys can no longer resolve to a path outside the storage directory; an invalid key now throws `InvalidKeyException` instead of silently reading/writing outside the intended folder.
- Automatic migration of legacy pre-0.3 plain-JSON `.jk` files is no longer performed. If you're upgrading from a 0.2.x installation, convert your `.jk` files before updating past 1.6.
- `Insert`, `Get`, and `Delete` now consistently wrap unexpected failures in `InvalidOperationException`, with the original exception preserved as `InnerException`.
