# JSONStorage

[![.NET](https://github.com/a6576171/JSONStorage/workflows/.NET/badge.svg)](https://github.com/a6576171/JSONStorage)
[![NuGet](https://buildstats.info/nuget/Isaac.FileStorage)](https://www.nuget.org/packages/Isaac.FileStorage/)

A tiny, dependency-light key/value file storage library for .NET. Give it a key and any object, and it serialises the object to BSON (binary JSON) and writes it to a `.j2k` file named after the key — no schema, no database, no manual (de)serialisation code.

Targets `net10.0`.

## Install

```bash
dotnet add package Isaac.FileStorage
```

The NuGet package ID is `Isaac.FileStorage`, but the code lives under the `Isaac.FileStorage.Lib` namespace:

```csharp
using Isaac.FileStorage.Lib;
```

## Quick start

```csharp
var store = new FileStorageEngine("data"); // creates "data" if it doesn't exist

store.Insert("user-42", new { Name = "Ada", Role = "Admin" });

var user = store.Get<dynamic>("user-42");

foreach (var key in store.GetAllKeys())
    Console.WriteLine(key);

store.Delete("user-42");
```

Each entry is stored as its own `<key>.j2k` file inside the storage directory. Keys map directly to file names, so anything that's a valid file name works as a key.

## API

| Member | Description |
| --- | --- |
| `FileStorageEngine(string dirPath)` | Opens (or creates) the storage directory at `dirPath`. `DirectoryPath` exposes the resolved full path. |
| `void Insert<T>(string key, T obj)` | Serialises `obj` to BSON and writes/overwrites `<key>.j2k`. Inserting under an existing key replaces its contents. |
| `T Get<T>(string key)` | Reads `<key>.j2k` and deserialises it into `T`. |
| `IEnumerable<string> GetAllKeys()` | Returns every key currently stored (i.e. every `.j2k` file's base name) in the storage directory. |
| `void Delete(string key)` | Deletes `<key>.j2k`. |

## Exceptions

All custom exceptions live in `Isaac.FileStorage.Lib.CustomExceptions`.

| Exception | Thrown by | When |
| --- | --- | --- |
| `EmptyKeyException` | `Insert`, `Get`, `Delete` | `key` is `null` or empty. |
| `InvalidKeyException` | `Insert`, `Get`, `Delete` | `key` would resolve to a path outside the storage directory (e.g. contains `..` traversal or is an absolute path). |
| `StorageKeyNotFoundException` | `Delete` | `key` doesn't exist. |
| `InvalidOperationException` | `Insert`, `Get`, `Delete` | An unexpected I/O or (de)serialisation failure occurred (e.g. corrupt file, type mismatch, file locked by another process). The original exception is preserved as `InnerException`. |

`FileStorageEngine`'s constructor also throws `ArgumentNullException`/`ArgumentException` if `dirPath` is `null` or empty.

## Notes

- Storage format is BSON, written to `.j2k` files (a name predating this README — think "JSON with a key", now binary).
- As of 2.0, keys are validated so they can never resolve to a path outside the storage directory.
- 2.0 dropped automatic migration of legacy pre-0.3 plain-JSON `.jk` files — if you're upgrading from one of those very old versions, convert your data before updating past 1.6.
