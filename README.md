# JSONStorage

[![.NET](https://github.com/a6576171/JSONStorage/workflows/.NET/badge.svg)](https://github.com/a6576171/JSONStorage)
[![NuGet](https://img.shields.io/nuget/v/Isaac.FileStorage)](https://www.nuget.org/packages/Isaac.FileStorage/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Isaac.FileStorage)](https://www.nuget.org/packages/Isaac.FileStorage/)

A tiny, dependency-light key/value file storage library for .NET. Give it a key and any object, and it serializes the object to BSON (binary JSON) and writes it to a `.j2k` file named after the key: no schema, no database, no manual (de)serialization code.

Targets `net10.0`.

## Install

```bash
dotnet add package Isaac.FileStorage
```

```csharp
using Isaac.FileStorage;
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
| `FileStorageEngine(string dirPath, TimeSpan? lockTimeout = null)` | Opens (or creates) the storage directory at `dirPath`. `DirectoryPath` exposes the resolved full path. `lockTimeout` (default 30s) caps how long a call waits to acquire a key's lock; must be non-negative and no more than 100 years. |
| `void Insert<T>(string key, T obj)` / `Task InsertAsync<T>(string key, T obj, CancellationToken ct = default)` | Serialises `obj` to BSON and writes/overwrites `<key>.j2k`. Inserting under an existing key replaces its contents. Writes atomically (via a temp file + rename), so a failure or cancellation partway through can never leave `<key>.j2k` truncated or partially overwritten — you keep either the old value or the fully-written new one. |
| `T Get<T>(string key)` / `Task<T> GetAsync<T>(string key, CancellationToken ct = default)` | Reads `<key>.j2k` and deserialises it into `T`. If the key doesn't exist, no trace of the attempt is left behind — see below. |
| `IEnumerable<string> GetAllKeys()` / `Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken ct = default)` | Returns every key currently stored (i.e. every `.j2k` file's base name) in the storage directory. |
| `void Delete(string key)` / `Task DeleteAsync(string key, CancellationToken ct = default)` | Deletes `<key>.j2k`. On success, also removes that key's sidecar `.lock` and any leftover `.tmp` file. |
| `(int LockFilesRemoved, int TempFilesRemoved) PruneOrphanedFiles()` | Removes stray sidecar files left behind by interrupted operations: `.lock` files with no corresponding `.j2k`, and `.tmp` files left over from an `Insert` that crashed between writing and its atomic rename. Not called automatically — call it yourself if you want to reclaim that space. Safe to call anytime, including mid-flight: a file currently in active use for its key is simply skipped and left for a future call. |

### Concurrency

`Insert`/`Get`/`Delete` (sync and async) are safe to call concurrently — from multiple threads *and* multiple processes — against the same key. Access to different keys never blocks each other. This is enforced by a per-key lock: an in-process `SemaphoreSlim` for cheap same-process waiting, plus an exclusively-held sidecar `<key>.j2k.lock` file for real cross-process exclusion. The async methods only get genuine non-blocking behavior on the lock wait and (for `Insert`) the write itself — Newtonsoft's BSON reader has no async API, so `GetAsync`'s deserialization step still runs synchronously once the lock is held.

**Known trade-offs, by design:**

- **Not reentrant.** If the same call chain re-enters a lock it's already holding for the same key (e.g. a custom serialization callback that calls back into `Insert`/`Get` for that key), it waits on itself and eventually throws `LockTimeoutException` rather than deadlocking forever. A fail-fast reentrancy guard was tried and dropped: it relied on `AsyncLocal`, which flows into `Task.Run` by default, making it indistinguishable from — and prone to misfiring on — the common, legitimate pattern of firing off independent concurrent work via `Task.Run` while already holding a lock.
- **`GetAllKeys()`/`GetAllKeysAsync()` aren't lock-protected.** Locking the whole directory for a listing would defeat the point of per-key parallelism, so the result is a point-in-time snapshot that can be stale by the time you act on it — the same way any directory listing is under concurrent modification. It's guaranteed to never throw due to concurrent inserts/deletes elsewhere, just not guaranteed to be exactly current.
- **Locks are held one per key that's ever been accessed, for as long as that key exists**, as a small `<key>.j2k.lock` sidecar file — this roughly doubles file count in the storage directory at scale. This is intentional: recreating/deleting the lock file on every single access would add I/O overhead for no benefit. Lock files are cleaned up automatically when their key is `Delete`d; `PruneOrphanedFiles()` handles the rest (stale ones left behind by out-of-band data deletion, or by versions before 2.1). A key that turns out **not** to have real data is a special case: `Insert`/`Get`/`Delete`/`PruneOrphanedFiles` all have to acquire the lock to safely check or attempt a write, which creates the lock file as a side effect — but they also clean that up immediately once they confirm there's no real data behind it (a failed `Insert` on a brand-new key, `Get`/`Delete` on a key that's never been created, or a stray temp file with no data of its own), so none of these leave anything behind.
- **Locking only protects access mediated through this library.** It can't protect against something else — another tool, a manual edit — touching the `.j2k` files directly.

## Exceptions

All custom exceptions live in `Isaac.FileStorage.CustomExceptions`.

| Exception | Thrown by | When |
| --- | --- | --- |
| `EmptyKeyException` | `Insert`, `Get`, `Delete` (sync and async) | `key` is `null` or empty. |
| `InvalidKeyException` | `Insert`, `Get`, `Delete` (sync and async) | `key` would resolve to a path outside the storage directory (e.g. contains `..` traversal or is an absolute path). |
| `StorageKeyNotFoundException` | `Delete` (sync and async) | `key` doesn't exist. |
| `LockTimeoutException` | `Insert`, `Get`, `Delete` (sync and async) | Waiting to acquire the key's lock exceeded `lockTimeout`. |
| `InvalidOperationException` | `Insert`, `Get`, `Delete` (sync and async) | An unexpected I/O or (de)serialisation failure occurred (e.g. corrupt file, type mismatch, file locked by another process). The original exception is preserved as `InnerException`. |

`FileStorageEngine`'s constructor also throws `ArgumentNullException`/`ArgumentException` if `dirPath` is `null` or empty.

## Notes

- Storage format is BSON, written to `.j2k` files.
- As of 2.0, keys are validated so they can never resolve to a path outside the storage directory.
- 2.0 dropped automatic migration of legacy pre-0.3 plain-JSON `.jk` files — if you're upgrading from one of those very old versions, convert your data before updating past 1.6.
- 2.1 added concurrency safety, the async API (`InsertAsync`/`GetAsync`/`DeleteAsync`/`GetAllKeysAsync`), atomic writes for `Insert`/`InsertAsync`, and `PruneOrphanedFiles()` — fully additive, no breaking changes from 2.0.
