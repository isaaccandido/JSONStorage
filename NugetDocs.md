# **JSONStorage**

A simple, dependency-light key/value file storage library. Give it a string key and any object, and it serializes the object with BSON (binary JSON) and writes it to a `.j2k` file named after the key: no schema, no database setup, no manual (de)serialization code.

## **Quick start**

```csharp
using Isaac.FileStorage;

var store = new FileStorageEngine("data"); // creates the directory if it doesn't exist

store.Insert("user-42", new { Name = "Ada", Role = "Admin" });

var user = store.Get<dynamic>("user-42");

foreach (var key in store.GetAllKeys())
    Console.WriteLine(key);

store.Delete("user-42");
```

## **Functionalities**

1. *`FileStorageEngine(string dirPath, TimeSpan? lockTimeout = null)`*: Opens (or creates) the storage directory at `dirPath`. The resolved full path is exposed via `DirectoryPath`. `lockTimeout` (default 30s) caps how long a call waits to acquire a key's lock; must be non-negative and no more than 100 years.
2. *`void Insert<T>(string key, T obj)`* / *`Task InsertAsync<T>(string key, T obj, CancellationToken ct = default)`*: Serialises `obj` and writes it to a file named after `key`. Inserting under an existing key overwrites it. Writes atomically (temp file + rename), so a failure or cancellation partway through never leaves `<key>.j2k` truncated or partially overwritten.
3. *`T Get<T>(string key)`* / *`Task<T> GetAsync<T>(string key, CancellationToken ct = default)`*: Reads the file named after `key` and deserializes it into `T`.
4. *`IEnumerable<string> GetAllKeys()`* / *`Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken ct = default)`*: Returns every key currently stored (every `.j2k` file's base name) in the storage directory.
5. *`void Delete(string key)`* / *`Task DeleteAsync(string key, CancellationToken ct = default)`*: Deletes the entry for `key`. On success, also removes that key's sidecar `.lock` and any leftover `.tmp` file.
6. *`(int LockFilesRemoved, int TempFilesRemoved) PruneOrphanedFiles()`*: Removes stray sidecar files left behind by interrupted operations: `.lock` files with no corresponding `.j2k`, and `.tmp` files left over from a crashed `Insert`. Not automatic; call it yourself to reclaim that space. Safe to call anytime.

### **Concurrency**

`Insert`/`Get`/`Delete` (sync and async) are safe to call concurrently from multiple threads and multiple processes against the same key; different keys never block each other. A per-key lock enforces this: an in-process `SemaphoreSlim` for cheap same-process waiting, plus an exclusively-held sidecar `<key>.j2k.lock` file for real cross-process exclusion.

Trade-offs, by design:

- **Not reentrant** — a call chain re-entering the same key's lock waits on itself and eventually throws `LockTimeoutException` rather than deadlocking forever. (A fail-fast `AsyncLocal`-based reentrancy guard was tried and dropped: `AsyncLocal` flows into `Task.Run` by default, which made it misfire on the common, legitimate pattern of firing off concurrent work via `Task.Run` while already holding a lock.)
- **`GetAllKeys()`/`GetAllKeysAsync()` are not lock-protected** — locking the whole directory would defeat per-key parallelism, so results are a point-in-time snapshot that can be stale by the time you act on it. Guaranteed to never throw due to concurrent modification elsewhere, just not guaranteed to be exactly current.
- **A `.lock` sidecar file persists for as long as its key exists**, roughly doubling file count at scale — intentional, since recreating/deleting it on every access would cost I/O for no benefit. `Delete` cleans up its own; `PruneOrphanedFiles()` handles anything left over from elsewhere. A key that turns out to have no real data is a special case: `Insert`/`Get`/`Delete`/`PruneOrphanedFiles` acquire the lock to safely check or attempt a write (creating the lock file as a side effect) but clean it back up immediately once they confirm there's no real data behind it - a failed `Insert` on a brand-new key, `Get`/`Delete` on a key that's never existed, or an orphaned temp file all leave nothing behind.
- **Locking only protects access mediated through this library** — it can't stop something else from touching the `.j2k` files directly.

## **Exceptions**

All custom exceptions live in `Isaac.FileStorage.CustomExceptions`.

- **`EmptyKeyException`**: thrown by `Insert`, `Get`, and `Delete` (sync and async) when `key` is `null` or empty.
- **`InvalidKeyException`**: thrown by `Insert`, `Get`, and `Delete` (sync and async) when `key` would resolve to a path outside the storage directory (e.g. `..` traversal or an absolute path).
- **`StorageKeyNotFoundException`**: thrown by `Delete` (sync and async) when `key` doesn't exist.
- **`LockTimeoutException`**: thrown by `Insert`, `Get`, and `Delete` (sync and async) when waiting to acquire the key's lock exceeds `lockTimeout`.
- **`InvalidOperationException`**: thrown by `Insert`, `Get`, and `Delete` (sync and async) for unexpected I/O or (de)serialisation failures (corrupt data, type mismatch, a file locked by another process, etc). The original exception is preserved as `InnerException`.

The constructor itself throws `ArgumentNullException`/`ArgumentException` if `dirPath` is `null` or empty.

---

## **IMPORTANT NOTE!**

From version 0.3 and above, this solution is BSON-based (binary JSON) rather than plain JSON. Faster to serialize/deserialize, and smaller on disk. Files are saved as `.j2k` (it used to be *a* joke, now it's two).

**Version 2.1 adds concurrency support (fully additive, no breaking changes from 2.0):**

- `Insert`, `Get`, and `Delete` are now safe to call concurrently from multiple threads and even multiple processes against the same key.
- `InsertAsync`, `GetAsync`, `DeleteAsync`, and `GetAllKeysAsync` were added.
- The constructor gained an optional `lockTimeout` parameter (default 30s); exceeding it throws the new `LockTimeoutException`.
- Added `PruneOrphanedFiles()` to reclaim space from stale sidecar lock and temp files.
- `Insert`/`InsertAsync` now write atomically (temp file + rename), so a crash, disk-full error, or cancelled `CancellationToken` partway through a write can no longer truncate or partially overwrite a key's existing data. A cancellation that arrives after the write finishes but before the rename still discards the new value rather than completing it.
- `Delete`/`DeleteAsync` now also clean up any leftover temp file for that key, not just its lock file.
- `Insert`, `InsertAsync`, `Get`, `GetAsync`, `Delete`, `DeleteAsync`, and `PruneOrphanedFiles` no longer leave a stray `.lock` file behind for a key that turns out to have no real data - a lock file created just to safely check or attempt a write is cleaned up immediately once that's confirmed (e.g. a failed `Insert` on a brand-new key).

**Version 2.0 is a breaking release:**

- The `KeyNotFoundException` custom exception was renamed to `StorageKeyNotFoundException` and moved to `Isaac.FileStorage.CustomExceptions`, to avoid colliding with `System.Collections.Generic.KeyNotFoundException`.
- A path-traversal issue in key handling was fixed. Keys can no longer resolve to a path outside the storage directory; an invalid key now throws `InvalidKeyException` instead of silently reading/writing outside the intended folder.
- Automatic migration of legacy pre-0.3 plain-JSON `.jk` files is no longer performed. If you're upgrading from a 0.2.x installation, convert your `.jk` files before updating past 1.6.
- `Insert`, `Get`, and `Delete` now consistently wrap unexpected failures in `InvalidOperationException`, with the original exception preserved as `InnerException`.
