using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Isaac.FileStorage.CustomExceptions;

namespace Isaac.FileStorage.Concurrency;

/// <summary>
/// Serialises access to a given file path across both threads (via an in-process
/// SemaphoreSlim) and processes (via an exclusively-held sidecar ".lock" file). Different
/// paths never block each other; the same path always resolves to the same wait queue.
/// The configured timeout bounds the total wait across both layers combined. Semaphore
/// entries are reference-counted and removed once nothing references them, so the
/// in-process lock table doesn't grow unboundedly over a long-running process's lifetime.
/// </summary>
/// <remarks>
/// Not reentrant: a call chain that re-enters the same path while already holding it will
/// wait on itself and eventually throw LockTimeoutException. An AsyncLocal-based fail-fast
/// reentrancy guard was tried and deliberately dropped - AsyncLocal state flows into
/// Task.Run by default, which made it indistinguishable from (and would misfire on) the
/// common, legitimate pattern of firing off independent concurrent work via Task.Run while
/// already holding a lock. A false positive there would be worse than the rarer genuine
/// reentrant deadlock, which the timeout already bounds and surfaces clearly.
/// </remarks>
internal static class KeyLock
{
    private const string LockFileSuffix = ".lock";
    private const int InitialRetryDelayMs = 10;
    private const int MaxRetryDelayMs = 200;

    // Deliberately short and not caller-configurable: pruning is a best-effort maintenance pass,
    // not a data operation, so it should never hang waiting on a key that's genuinely busy right
    // now - it just skips that file and picks it up again on a future prune call.
    private static readonly TimeSpan PruneAcquireTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly ConcurrentDictionary<string, RefCountedSemaphore> InProcessLocks = new();

    public static LockHandle Acquire(string fileName, TimeSpan timeout) =>
        Acquire(fileName, timeout, createIfMissing: true);

    /// <summary>
    /// Like Acquire, but for a lock file that must already exist: throws FileNotFoundException
    /// instead of transparently creating one. Used by PruneOrphanedFiles's lock-file cleanup,
    /// where silently recreating a just-deleted file would let a racing prune call re-claim and
    /// re-count it - see the remarks on PruneOrphanedFiles for the full reasoning.
    /// </summary>
    private static LockHandle AcquireExisting(string fileName, TimeSpan timeout) =>
        Acquire(fileName, timeout, createIfMissing: false);

    private static LockHandle Acquire(string fileName, TimeSpan timeout, bool createIfMissing)
    {
        var deadline = DateTime.UtcNow + timeout;
        var entry = Rent(fileName);

        bool acquired;
        try
        {
            acquired = entry.Semaphore.Wait(RemainingTime(deadline));
        }
        catch
        {
            // e.g. ObjectDisposedException - we never got the permit, so don't release it, just untrack.
            Return(fileName, entry);
            throw;
        }

        if (!acquired)
        {
            Return(fileName, entry);
            throw new LockTimeoutException();
        }

        try
        {
            var lockStream = AcquireFileLock(fileName, deadline, createIfMissing);
            return new LockHandle(fileName, entry, lockStream);
        }
        catch
        {
            entry.Semaphore.Release();
            Return(fileName, entry);
            throw;
        }
    }

    public static async Task<LockHandle> AcquireAsync(string fileName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        var entry = Rent(fileName);

        bool acquired;
        try
        {
            acquired = await entry.Semaphore.WaitAsync(RemainingTime(deadline), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // WaitAsync throws (rather than returning false) on cancellation - we never got the
            // permit, so don't release it, just untrack. Without this, a cancelled caller would
            // permanently leak this key's entry, since RefCount never makes it back to zero.
            Return(fileName, entry);
            throw;
        }

        if (!acquired)
        {
            Return(fileName, entry);
            throw new LockTimeoutException();
        }

        try
        {
            var lockStream = await AcquireFileLockAsync(fileName, deadline, cancellationToken).ConfigureAwait(false);
            return new LockHandle(fileName, entry, lockStream);
        }
        catch
        {
            entry.Semaphore.Release();
            Return(fileName, entry);
            throw;
        }
    }

    /// <summary>
    /// Best-effort removal of a key's sidecar lock file once its data is gone for good. Must
    /// only be called while the caller still holds that same key's own lock (i.e. from inside
    /// the Acquire/AcquireAsync critical section, before releasing) - the lock file's handle is
    /// opened with FileShare.Delete specifically so this succeeds without needing to release
    /// first. Calling it after releasing would reopen a race this precondition exists to close:
    /// a concurrent acquirer could be mid-use of the file by then, and on POSIX systems
    /// unlink() succeeds regardless of any lock another handle holds, so deleting out from under
    /// it wouldn't even fail - a third caller could then recreate and "exclusively" acquire the
    /// same path while the original holder is still relying on it.
    /// </summary>
    public static void TryDeleteLockFile(string fileName)
    {
        try
        {
            File.Delete(fileName + LockFileSuffix);
        }
        catch
        {
            // best effort - an orphaned lock file is harmless
        }
    }

    /// <summary>
    /// Removes stray sidecar files left behind by interrupted operations: lock files with no
    /// corresponding data file, and temp files left over from a Insert that crashed between
    /// writing and its atomic rename. Not called automatically. Safe to call at any time,
    /// including while other operations are in flight.
    /// </summary>
    /// <remarks>
    /// Neither lock nor temp files are safe to delete on sight without first proving exclusive
    /// access to them, and that delete must happen while still holding it - not after releasing.
    /// For temp files, Insert closes the file between finishing the write and performing the
    /// rename, so a temp file can transiently exist on disk with no handle open on it while still
    /// belonging to an in-flight Insert - deleting it directly could race that gap and pull it out
    /// from under that Insert right before its rename. Acquiring the key's own lock first, via
    /// the normal Acquire (which recreates the lock file if it's missing - fine here, since it's
    /// the temp file being examined and deleted, a different file entirely), and deleting before
    /// releasing, closes that gap.
    ///
    /// Lock files need a different approach specifically because they're what Acquire itself
    /// manages: Acquire's FileMode.OpenOrCreate would transparently recreate a lock file that a
    /// racing prune call just deleted, so two concurrent prune calls could each legitimately (but
    /// wrongly) conclude they removed a real orphan - the second one's "proof" would really just
    /// be the file it recreated moments earlier as a side effect of checking. AcquireExisting
    /// closes that gap by using FileMode.Open (never Create): if the file's already gone by the
    /// time this runs - deleted by a racing prune call - it throws and the attempt is skipped
    /// rather than silently recreating and re-claiming it.
    ///
    /// This still goes through the same in-process SemaphoreSlim as every other Acquire call,
    /// not just the file-level FileShare.Delete check - that's not optional. An earlier version
    /// of this method opened the FileStream directly, skipping the semaphore, on the reasoning
    /// that FileShare.Delete alone should already provide equivalent exclusivity; that held up
    /// under heavy same-process contention on Windows but not reliably on Linux (confirmed via
    /// CI: concurrent prune calls still occasionally each won a claim on the same file). Routing
    /// through the semaphore first - the same layer every other concurrent-access guarantee in
    /// this codebase actually rests on - removes the dependency on that platform-specific edge
    /// entirely, rather than needing to reason about exactly how reliable it is.
    /// </remarks>
    /// <returns>How many orphaned lock files and temp files were removed.</returns>
    public static (int LockFilesRemoved, int TempFilesRemoved) PruneOrphanedFiles(
        string directoryPath, string dataFileExtension, string tempFileExtension)
    {
        var lockFilesRemoved = 0;
        var tempFilesRemoved = 0;

        foreach (var lockFilePath in Directory.GetFiles(directoryPath, $"*{dataFileExtension}{LockFileSuffix}"))
        {
            var dataFilePath = lockFilePath[..^LockFileSuffix.Length];
            if (File.Exists(dataFilePath)) continue;

            try
            {
                using (AcquireExisting(dataFilePath, PruneAcquireTimeout))
                {
                    if (!File.Exists(dataFilePath))
                    {
                        File.Delete(lockFilePath);
                        lockFilesRemoved++;
                    }
                }
            }
            catch
            {
                // already gone (claimed by a racing prune call), or actively in use by a real
                // operation right now (lock timed out) - leave it for a future attempt either way
            }
        }

        foreach (var tempFilePath in Directory.GetFiles(directoryPath, $"*{dataFileExtension}{tempFileExtension}"))
        {
            var dataFilePath = tempFilePath[..^tempFileExtension.Length];

            try
            {
                using (Acquire(dataFilePath, PruneAcquireTimeout))
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                        tempFilesRemoved++;

                        // Acquiring the lock to safely touch the temp file may have just created
                        // a lock file for this key. If it has no real data either, clean that up
                        // too - still while holding the lock, so nothing can be displaced by it.
                        if (!File.Exists(dataFilePath)) TryDeleteLockFile(dataFilePath);
                    }
                }
            }
            catch
            {
                // key actively in use right now (lock timed out), or delete failed - leave it for a future attempt
            }
        }

        return (lockFilesRemoved, tempFilesRemoved);
    }

    /// <summary>Test-only diagnostic hook: whether the in-process lock table currently has an entry for this path.</summary>
    internal static bool IsTracked(string fileName) => InProcessLocks.ContainsKey(fileName);

    private static RefCountedSemaphore Rent(string fileName)
    {
        while (true)
        {
            var entry = InProcessLocks.GetOrAdd(fileName, _ => new RefCountedSemaphore());

            lock (entry)
            {
                if (entry.RefCount < 0) continue; // tombstoned by a concurrent Return; retry with a fresh entry

                entry.RefCount++;
                return entry;
            }
        }
    }

    private static void Return(string fileName, RefCountedSemaphore entry)
    {
        lock (entry)
        {
            entry.RefCount--;

            if (entry.RefCount != 0) return;
            InProcessLocks.TryRemove(fileName, out _);
            entry.RefCount = int.MinValue; // tombstone: any racing Rent must not reuse this instance
        }
    }

    private static TimeSpan RemainingTime(DateTime deadline)
    {
        var remaining = deadline - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero) return TimeSpan.Zero;

        // SemaphoreSlim.Wait/WaitAsync reject TimeSpans beyond int.MaxValue milliseconds.
        var maxSupported = TimeSpan.FromMilliseconds(int.MaxValue);
        return remaining > maxSupported ? maxSupported : remaining;
    }

    // FileShare.Delete (rather than FileShare.None) still blocks every other Acquire/AcquireAsync
    // call for the same path - they all request FileAccess.ReadWrite, which isn't among what's
    // shared - but it additionally permits File.Delete to succeed against this exact handle while
    // it's still open. That's what lets TryDeleteLockFile run from inside the critical section
    // that's already proven the file is orphaned, instead of needing to release first and reopen
    // a window for a concurrent acquirer to be displaced by that cleanup.
    private static FileStream AcquireFileLock(string fileName, DateTime deadline, bool createIfMissing = true)
    {
        var lockFilePath = fileName + LockFileSuffix;
        var delay = InitialRetryDelayMs;
        var mode = createIfMissing ? FileMode.OpenOrCreate : FileMode.Open;

        while (true)
        {
            try
            {
                return new FileStream(lockFilePath, mode, FileAccess.ReadWrite, FileShare.Delete);
            }
            catch (FileNotFoundException) when (!createIfMissing)
            {
                // The caller asked not to create one, and there's genuinely nothing there right
                // now (as opposed to something else holding it) - not worth retrying until the
                // deadline for this, unlike a real contention IOException below.
                throw;
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= deadline) throw new LockTimeoutException();

                Thread.Sleep(delay);
                delay = Math.Min(delay * 2, MaxRetryDelayMs);
            }
        }
    }

    private static async Task<FileStream> AcquireFileLockAsync(string fileName, DateTime deadline, CancellationToken cancellationToken)
    {
        var lockFilePath = fileName + LockFileSuffix;
        var delay = InitialRetryDelayMs;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Delete);
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= deadline) throw new LockTimeoutException();

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = Math.Min(delay * 2, MaxRetryDelayMs);
            }
        }
    }

    internal sealed class RefCountedSemaphore
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    internal sealed class LockHandle(string fileName, RefCountedSemaphore entry, FileStream lockStream) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            try
            {
                lockStream.Dispose();
            }
            finally
            {
                entry.Semaphore.Release();
                Return(fileName, entry);
            }
        }
    }
}
