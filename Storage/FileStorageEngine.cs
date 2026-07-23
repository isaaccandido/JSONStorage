using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Isaac.FileStorage.Concurrency;
using Isaac.FileStorage.CustomExceptions;
using Isaac.FileStorage.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Isaac.FileStorage;

public class FileStorageEngine
{
    private static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(30);

    // Comfortably below the point where DateTime.UtcNow + lockTimeout would overflow
    // DateTime's range - "effectively infinite" for any real use case.
    private static readonly TimeSpan MaxLockTimeout = TimeSpan.FromDays(365 * 100);

    private readonly TimeSpan _lockTimeout;

    public string DirectoryPath { get; }

    /// <param name="dirPath">The directory to store entries in. Created if it doesn't exist.</param>
    /// <param name="lockTimeout">
    /// How long Insert/Get/Delete wait to acquire a key's lock before throwing
    /// LockTimeoutException. Defaults to 30 seconds. Only same-key access contends;
    /// different keys never block each other. Must be non-negative and no more than 100 years.
    /// </param>
    public FileStorageEngine(string dirPath, TimeSpan? lockTimeout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(dirPath);

        if (lockTimeout is { } timeout)
        {
            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(lockTimeout), timeout, "lockTimeout must not be negative.");
            if (timeout > MaxLockTimeout)
                throw new ArgumentOutOfRangeException(nameof(lockTimeout), timeout, $"lockTimeout must not exceed {MaxLockTimeout}.");
        }

        var di = new DirectoryInfo(dirPath);
        if (!di.Exists) di.Create();
        DirectoryPath = di.FullName;

        _lockTimeout = lockTimeout ?? DefaultLockTimeout;
    }

    /// <summary>
    /// Inserts an entry and records it to a file.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="key">The file key (will be used as file name).</param>
    /// <param name="obj">The instantiated class containing data.</param>
    /// <remarks>
    /// Writes to a sibling temp file and atomically renames it over the destination, so a
    /// failure or interruption partway through (crash, disk full, cancellation) can never leave
    /// the key's existing data truncated or partially overwritten - either the old content or
    /// the fully-written new content, never a mix of the two.
    /// </remarks>
    public void Insert<T>(string key, T? obj)
    {
        if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

        var fileName = GetFileName(key);
        var tempFileName = fileName + Constants.TempFileExtension;
        Exception? failure = null;

        using (KeyLock.Acquire(fileName, _lockTimeout))
        {
            try
            {
                var bytes = Bson.Generate(obj);
                File.WriteAllBytes(tempFileName, bytes);
                File.Move(tempFileName, fileName, overwrite: true);
            }
            catch (Exception ex)
            {
                TryDeleteTempFile(tempFileName);
                failure = new InvalidOperationException(
                    $"Cannot insert data content for key '{key}'. " +
                    $"This happened because the object of type '{typeof(T)}' could not be serialised or the file could not be written. " +
                    "Try verifying the object you are trying to store and try again.", ex);
            }

            // A failed insert for a key that still has no real data (never succeeded before, and
            // didn't just now either) should leave nothing behind - not even the lock file that
            // had to be acquired to attempt the write. Done while still holding the lock, so
            // there's no gap for a concurrent acquirer to be displaced by this cleanup.
            if (failure != null && !File.Exists(fileName)) KeyLock.TryDeleteLockFile(fileName);
        }

        if (failure != null) throw failure;
    }

    /// <summary>
    /// Asynchronously inserts an entry and records it to a file.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="key">The file key (will be used as file name).</param>
    /// <param name="obj">The instantiated class containing data.</param>
    /// <param name="cancellationToken">Cancels waiting for the lock or the write itself.</param>
    /// <remarks>
    /// Writes to a sibling temp file and atomically renames it over the destination, so a
    /// failure or interruption partway through (crash, disk full, cancellation) can never leave
    /// the key's existing data truncated or partially overwritten - either the old content or
    /// the fully-written new content, never a mix of the two.
    /// </remarks>
    public async Task InsertAsync<T>(string key, T? obj, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

        var fileName = GetFileName(key);
        var tempFileName = fileName + Constants.TempFileExtension;
        ExceptionDispatchInfo? failure = null;

        var lockHandle = await KeyLock.AcquireAsync(fileName, _lockTimeout, cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                var bytes = Bson.Generate(obj);
                await File.WriteAllBytesAsync(tempFileName, bytes, cancellationToken).ConfigureAwait(false);

                // Even though the write itself finished successfully, a cancellation that arrives in
                // this exact window must still discard it rather than complete the insert.
                cancellationToken.ThrowIfCancellationRequested();

                File.Move(tempFileName, fileName, overwrite: true);
            }
            catch (OperationCanceledException ex)
            {
                TryDeleteTempFile(tempFileName);
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            catch (Exception ex)
            {
                TryDeleteTempFile(tempFileName);
                failure = ExceptionDispatchInfo.Capture(new InvalidOperationException(
                    $"Cannot insert data content for key '{key}'. " +
                    $"This happened because the object of type '{typeof(T)}' could not be serialised or the file could not be written. " +
                    "Try verifying the object you are trying to store and try again.", ex));
            }

            // A failed insert (including a cancelled one) for a key that still has no real data
            // should leave nothing behind - not even the lock file that had to be acquired to
            // attempt the write. Done while still holding the lock, so there's no gap for a
            // concurrent acquirer to be displaced by this cleanup.
            if (failure != null && !File.Exists(fileName)) KeyLock.TryDeleteLockFile(fileName);
        }
        finally
        {
            lockHandle.Dispose();
        }

        failure?.Throw();
    }

    private static void TryDeleteTempFile(string tempFileName)
    {
        try
        {
            File.Delete(tempFileName);
        }
        catch
        {
            // best effort - an orphaned temp file is harmless and gets overwritten on the next insert for this key
        }
    }

    /// <summary>
    /// Retrieves an entry from file.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="key">The file key (the file name).</param>
    /// <returns>The deserialized value, or null if the key's stored value was itself null.</returns>
    public T? Get<T>(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

        var fileName = GetFileName(key);
        T? result = default;
        Exception? failure = null;

        using (KeyLock.Acquire(fileName, _lockTimeout))
        {
            var keyExists = File.Exists(fileName);

            try
            {
                using var fs = File.OpenRead(fileName);
                using var reader = new BsonDataReader(fs);

                result = new JsonSerializer().Deserialize<T>(reader);
            }
            catch (Exception ex)
            {
                failure = new InvalidOperationException(
                    $"Cannot get data content from file of key '{key}'. " +
                    "This happened because either the file is unreadable or the generic type mismatches. " +
                    $"The current destination type is '{typeof(T)}' but I'm unable to determine the actual type. " +
                    "Try verifying the type you are trying to recover data to and try again.", ex);
            }

            // A key that turns out not to exist should leave nothing behind - not even the lock
            // file that had to be acquired to safely check. Done while still holding the lock, so
            // there's no gap for a concurrent acquirer to be displaced by this cleanup.
            if (!keyExists) KeyLock.TryDeleteLockFile(fileName);
        }

        return failure != null 
            ? throw failure 
            : result;
    }

    /// <summary>
    /// Asynchronously retrieves an entry from file.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="key">The file key (the file name).</param>
    /// <param name="cancellationToken">Cancels waiting for the lock.</param>
    /// <returns>The deserialized value, or null if the key's stored value was itself null.</returns>
    /// <remarks>
    /// Newtonsoft's BSON reader has no async API, so only lock acquisition is genuinely
    /// non-blocking here; deserialization itself still runs synchronously once the lock is held.
    /// </remarks>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

        var fileName = GetFileName(key);
        T? result = default;
        ExceptionDispatchInfo? failure = null;

        var lockHandle = await KeyLock.AcquireAsync(fileName, _lockTimeout, cancellationToken).ConfigureAwait(false);
        try
        {
            var keyExists = File.Exists(fileName);

            try
            {
                await using var fs = File.OpenRead(fileName);
                await using var reader = new BsonDataReader(fs);

                result = new JsonSerializer().Deserialize<T>(reader);
            }
            catch (OperationCanceledException ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(new InvalidOperationException(
                    $"Cannot get data content from file of key '{key}'. " +
                    "This happened because either the file is unreadable or the generic type mismatches. " +
                    $"The current destination type is '{typeof(T)}' but I'm unable to determine the actual type. " +
                    "Try verifying the type you are trying to recover data to and try again.", ex));
            }

            // A key that turns out not to exist should leave nothing behind - not even the lock
            // file that had to be acquired to safely check. Done while still holding the lock, so
            // there's no gap for a concurrent acquirer to be displaced by this cleanup. Capturing
            // (rather than filtering out) OperationCanceledException above, unlike the simpler
            // try/catch elsewhere, means this cleanup still runs even if cancellation is what
            // ended the attempt - ExceptionDispatchInfo replays it afterward with its trace intact.
            if (!keyExists) KeyLock.TryDeleteLockFile(fileName);
        }
        finally
        {
            lockHandle.Dispose();
        }

        failure?.Throw();
        return result;
    }

    /// <summary>
    /// Gets all keys matching file type on a given directory.
    /// </summary>
    /// <returns>An array containing all keys found.</returns>
    public IEnumerable<string> GetAllKeys()
    {
        return Directory.GetFiles(DirectoryPath, $"*{Constants.J2KFileExtension}")
            .Select(item => new FileInfo(item).Name[..^Constants.J2KFileExtension.Length]);
    }

    /// <summary>
    /// Asynchronously gets all keys matching file type on a given directory.
    /// </summary>
    /// <remarks>.NET has no async directory-enumeration API, so this only offers signature symmetry with the other Async methods.</remarks>
    public Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetAllKeys());
    }

    /// <summary>
    /// Removes stray sidecar files left behind by interrupted operations: lock files with no
    /// corresponding data file (e.g. data deleted by something other than this library, or left
    /// over from before 2.1), and temp files left over from an Insert that crashed between
    /// writing and its atomic rename. Not called automatically; call this yourself if you want to
    /// reclaim that space. Safe to call at any time, including while other operations are in
    /// flight - a file currently in active use is simply skipped and left for a future call.
    /// </summary>
    /// <returns>How many orphaned lock files and temp files were removed.</returns>
    public (int LockFilesRemoved, int TempFilesRemoved) PruneOrphanedFiles() =>
        KeyLock.PruneOrphanedFiles(DirectoryPath, Constants.J2KFileExtension, Constants.TempFileExtension);

    /// <summary>
    /// Removes an entry by key.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    public void Delete(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

        var fileName = GetFileName(key);
        var deleted = false;
        Exception? failure = null;

        using (KeyLock.Acquire(fileName, _lockTimeout))
        {
            var keyExists = File.Exists(fileName);

            if (!keyExists)
            {
                failure = new StorageKeyNotFoundException();
            }
            else
            {
                try
                {
                    File.Delete(fileName);
                    deleted = true;
                }
                catch (Exception ex)
                {
                    failure = new InvalidOperationException(
                        $"Cannot delete data content for key '{key}'. " +
                        "This happened because the file could not be deleted, e.g. it may be locked or access may be denied. " +
                        "Try verifying the file is not in use and try again.", ex);
                }

                // Still holding the lock here, so this can't race a concurrent Insert's own temp file.
                if (deleted) TryDeleteTempFile(fileName + Constants.TempFileExtension);
            }

            // Covers both a successful delete and a key that turned out not to exist - either way,
            // nothing should be left behind, not even the lock file that had to be acquired to
            // check. Done while still holding the lock, so there's no gap for a concurrent
            // acquirer to be displaced by this cleanup.
            if (deleted || !keyExists) KeyLock.TryDeleteLockFile(fileName);
        }

        if (failure != null) throw failure;
    }

    /// <summary>
    /// Asynchronously removes an entry by key.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <param name="cancellationToken">Cancels waiting for the lock.</param>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key)) throw new EmptyKeyException();

        var fileName = GetFileName(key);
        var deleted = false;
        ExceptionDispatchInfo? failure = null;

        var lockHandle = await KeyLock.AcquireAsync(fileName, _lockTimeout, cancellationToken).ConfigureAwait(false);
        try
        {
            var keyExists = File.Exists(fileName);

            if (!keyExists)
            {
                failure = ExceptionDispatchInfo.Capture(new StorageKeyNotFoundException());
            }
            else
            {
                try
                {
                    File.Delete(fileName);
                    deleted = true;
                }
                catch (OperationCanceledException ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(new InvalidOperationException(
                        $"Cannot delete data content for key '{key}'. " +
                        "This happened because the file could not be deleted, e.g. it may be locked or access may be denied. " +
                        "Try verifying the file is not in use and try again.", ex));
                }

                // Still holding the lock here, so this can't race a concurrent InsertAsync's own temp file.
                if (deleted) TryDeleteTempFile(fileName + Constants.TempFileExtension);
            }

            // Covers both a successful delete and a key that turned out not to exist - either way,
            // nothing should be left behind, not even the lock file that had to be acquired to
            // check. Done while still holding the lock, so there's no gap for a concurrent
            // acquirer to be displaced by this cleanup. Capturing cancellation too (rather than
            // filtering it out) means this cleanup still runs even if that's what ended the attempt.
            if (deleted || !keyExists) KeyLock.TryDeleteLockFile(fileName);
        }
        finally
        {
            lockHandle.Dispose();
        }

        failure?.Throw();
    }

    private string GetFileName(string key)
    {
        var fileName = Path.GetFullPath(Path.Combine(DirectoryPath, $"{key}{Constants.J2KFileExtension}"));
        var relativePath = Path.GetRelativePath(DirectoryPath, fileName);

        var escapesDirectory = relativePath == ".." ||
                                relativePath.StartsWith($"..{Path.DirectorySeparatorChar}") ||
                                Path.IsPathRooted(relativePath);

        return escapesDirectory 
            ? throw new InvalidKeyException() 
            : fileName;
    }
}
