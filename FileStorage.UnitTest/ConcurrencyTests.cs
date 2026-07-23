using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Isaac.FileStorage;
using Isaac.FileStorage.CustomExceptions;
using Xunit;

namespace FileStorage.UnitTest;

public class ConcurrencyTests
{
    [Fact]
    public async Task InsertAsync_GetAsync_RoundTripsCorrectly()
    {
        using var block = new TestBlock();

        await block.Db.InsertAsync("async-key", new TestClass { Code = "1", Name = "1" });

        var item = await block.Db.GetAsync<TestClass>("async-key");

        Assert.Equal("1", item.Code);
        Assert.Equal("1", item.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        using var block = new TestBlock();

        await block.Db.InsertAsync("async-delete", new TestClass { Code = "1", Name = "1" });
        await block.Db.DeleteAsync("async-delete");

        Assert.Empty(await block.Db.GetAllKeysAsync());
    }

    [Fact]
    public void Delete_AlsoRemovesTheSidecarLockFile()
    {
        using var block = new TestBlock();

        block.Db.Insert("cleanup-me", new TestClass { Code = "1", Name = "1" });
        var lockFilePath = Path.Combine(block.Db.DirectoryPath, "cleanup-me.j2k.lock");
        Assert.True(File.Exists(lockFilePath));

        block.Db.Delete("cleanup-me");

        Assert.False(File.Exists(lockFilePath));
    }

    [Fact]
    public async Task DeleteAsync_AlsoRemovesTheSidecarLockFile()
    {
        using var block = new TestBlock();

        await block.Db.InsertAsync("cleanup-me-async", new TestClass { Code = "1", Name = "1" });
        var lockFilePath = Path.Combine(block.Db.DirectoryPath, "cleanup-me-async.j2k.lock");
        Assert.True(File.Exists(lockFilePath));

        await block.Db.DeleteAsync("cleanup-me-async");

        Assert.False(File.Exists(lockFilePath));
    }

    [Fact]
    public void Delete_AlsoRemovesALeftoverTempFileForThatKey()
    {
        using var block = new TestBlock();

        block.Db.Insert("cleanup-temp-key", new TestClass { Code = "1", Name = "1" });

        // Simulate a temp file left behind by an earlier Insert attempt for this same key that
        // crashed between writing and its atomic rename.
        var tempPath = Path.Combine(block.Db.DirectoryPath, "cleanup-temp-key.j2k.tmp");
        File.WriteAllText(tempPath, "leftover-from-a-crash");

        block.Db.Delete("cleanup-temp-key");

        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public async Task DeleteAsync_AlsoRemovesALeftoverTempFileForThatKey()
    {
        using var block = new TestBlock();

        await block.Db.InsertAsync("cleanup-temp-key-async", new TestClass { Code = "1", Name = "1" });

        var tempPath = Path.Combine(block.Db.DirectoryPath, "cleanup-temp-key-async.j2k.tmp");
        await File.WriteAllTextAsync(tempPath, "leftover-from-a-crash");

        await block.Db.DeleteAsync("cleanup-temp-key-async");

        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public async Task DeleteAsync_KeyNotFound_ThrowsStorageKeyNotFoundException()
    {
        using var block = new TestBlock();

        await Assert.ThrowsAsync<StorageKeyNotFoundException>(() => block.Db.DeleteAsync("nope"));
    }

    [Fact]
    public async Task DeleteAsync_KeyNotFound_LeavesNoLockFileBehind()
    {
        using var block = new TestBlock();

        await Assert.ThrowsAsync<StorageKeyNotFoundException>(() => block.Db.DeleteAsync("nope-no-lock"));

        Assert.False(File.Exists(Path.Combine(block.Db.DirectoryPath, "nope-no-lock.j2k.lock")));
    }

    [Fact]
    public async Task GetAsync_InexistentKey_LeavesNoLockFileBehind()
    {
        using var block = new TestBlock();

        await Assert.ThrowsAsync<InvalidOperationException>(() => block.Db.GetAsync<TestClass>("nope-get-async"));

        Assert.False(File.Exists(Path.Combine(block.Db.DirectoryPath, "nope-get-async.j2k.lock")));
    }

    [Fact]
    public void PruneOrphanedFiles_RemovingAStrayTempFile_DoesNotLeaveANewLockFileBehind()
    {
        // Safely deleting a temp file requires acquiring that key's lock first, which creates
        // the lock file as a side effect. If the key turns out to have no real data either,
        // that side effect must be cleaned up too, so a single prune call leaves the directory
        // fully clean rather than trading one stray file for another.
        using var block = new TestBlock();

        var tempPath = Path.Combine(block.Db.DirectoryPath, "never-touched.j2k.tmp");
        File.WriteAllText(tempPath, "planted-by-something-else");

        var (lockFilesRemoved, tempFilesRemoved) = block.Db.PruneOrphanedFiles();

        Assert.Equal(0, lockFilesRemoved);
        Assert.Equal(1, tempFilesRemoved);
        Assert.Empty(Directory.GetFiles(block.Db.DirectoryPath));
    }

    [Fact]
    public async Task GetAllKeysAsync_ReflectsInsertedKeys()
    {
        using var block = new TestBlock();

        await block.Db.InsertAsync("a", new TestClass { Code = "1", Name = "1" });
        await block.Db.InsertAsync("b", new TestClass { Code = "2", Name = "2" });

        var keys = (await block.Db.GetAllKeysAsync()).ToArray();

        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public async Task InsertAsync_EmptyKey_ThrowsEmptyKeyException()
    {
        using var block = new TestBlock();

        await Assert.ThrowsAsync<EmptyKeyException>(() =>
            block.Db.InsertAsync(string.Empty, new TestClass { Code = "1", Name = "1" }));
    }

    [Fact]
    public async Task InsertAsync_PathTraversalKey_ThrowsInvalidKeyException()
    {
        using var block = new TestBlock();

        await Assert.ThrowsAsync<InvalidKeyException>(() =>
            block.Db.InsertAsync("../evil", new TestClass { Code = "1", Name = "1" }));
    }

    [Fact]
    public async Task InsertAsync_CancelledBeforeWriteCompletes_LeavesExistingDataIntact()
    {
        using var block = new TestBlock();

        await block.Db.InsertAsync("atomic-key-async", new TestClass { Code = "original", Name = "original" });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            block.Db.InsertAsync("atomic-key-async", new TestClass { Code = "new", Name = "new" }, cts.Token));

        var item = block.Db.Get<TestClass>("atomic-key-async");
        Assert.Equal("original", item.Code);
        Assert.Equal("original", item.Name);
    }

    [Fact]
    public async Task InsertAsync_Cancelled_CleansUpTempFile()
    {
        using var block = new TestBlock();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            block.Db.InsertAsync("cancelled-key", new TestClass { Code = "1", Name = "1" }, cts.Token));

        Assert.False(File.Exists(Path.Combine(block.Db.DirectoryPath, "cancelled-key.j2k.tmp")));
    }

    [Fact]
    public async Task InsertAsync_CancelledOnBrandNewKey_LeavesNoLockFileBehind()
    {
        using var block = new TestBlock();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            block.Db.InsertAsync("cancelled-no-lock", new TestClass { Code = "1", Name = "1" }, cts.Token));

        Assert.Empty(Directory.GetFiles(block.Db.DirectoryPath));
    }

    [Fact]
    public async Task InsertAsync_CancelledException_PreservesOriginalCancellationToken()
    {
        // The cleanup rework must not swap the propagated exception for a generic one - it should
        // still be recognizably tied to the token that was actually cancelled.
        using var block = new TestBlock();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            block.Db.InsertAsync("cancelled-token-check", new TestClass { Code = "1", Name = "1" }, cts.Token));

        Assert.Equal(cts.Token, ex.CancellationToken);
    }

    [Fact]
    public void GetAllKeys_IgnoresTempFiles()
    {
        using var block = new TestBlock();

        block.Db.Insert("real-key", new TestClass { Code = "1", Name = "1" });
        File.WriteAllText(Path.Combine(block.Db.DirectoryPath, "stray.j2k.tmp"), "leftover");

        var keys = block.Db.GetAllKeys().ToArray();

        Assert.Single(keys);
        Assert.Equal("real-key", keys[0]);
    }

    [Fact]
    public async Task ConcurrentInsertsOnSameKey_AllSucceedWithoutCorruption()
    {
        using var block = new TestBlock();

        var tasks = Enumerable.Range(0, 50)
            .Select(i => block.Db.InsertAsync("contended", new TestClass { Code = i.ToString(), Name = "x" }))
            .ToArray();

        await Task.WhenAll(tasks);

        // Whichever write landed last, the file must be intact and readable - not torn/corrupted.
        var result = await block.Db.GetAsync<TestClass>("contended");
        Assert.Equal("x", result.Name);
    }

    [Fact]
    public async Task ConcurrentAccessOnDifferentKeys_DoesNotBlockEachOther()
    {
        using var block = new TestBlock();

        var tasks = Enumerable.Range(0, 20)
            .Select(i => block.Db.InsertAsync($"key-{i}", new TestClass { Code = i.ToString(), Name = "x" }))
            .ToArray();

        await Task.WhenAll(tasks);

        var keys = (await block.Db.GetAllKeysAsync()).ToArray();
        Assert.Equal(20, keys.Length);
    }

    [Fact]
    public async Task Insert_WhenLockFileHeldByAnotherHolder_WaitsThenSucceedsOnceReleased()
    {
        var dirPath = "Tests_" + Guid.NewGuid();
        Directory.CreateDirectory(dirPath);

        try
        {
            var db = new FileStorageEngine(dirPath, TimeSpan.FromSeconds(5));
            var lockFilePath = Path.Combine(dirPath, "contended.j2k.lock");

            var externalHold = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            var insertTask = Task.Run(() => db.Insert("contended", new TestClass { Code = "1", Name = "1" }));

            // Give Insert a moment to reach the held lock and start its retry loop.
            await Task.Delay(200);
            Assert.False(insertTask.IsCompleted);

            await externalHold.DisposeAsync();

            await insertTask;

            var item = db.Get<TestClass>("contended");
            Assert.Equal("1", item.Code);
        }
        finally
        {
            Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public void Insert_LockHeldLongerThanTimeout_ThrowsLockTimeoutException()
    {
        var dirPath = "Tests_" + Guid.NewGuid();
        Directory.CreateDirectory(dirPath);

        try
        {
            var db = new FileStorageEngine(dirPath, TimeSpan.FromMilliseconds(200));
            var lockFilePath = Path.Combine(dirPath, "stuck.j2k.lock");

            using var externalHold = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            Assert.Throws<LockTimeoutException>(() =>
                db.Insert("stuck", new TestClass { Code = "1", Name = "1" }));
        }
        finally
        {
            Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public async Task InsertAsync_LockHeldLongerThanTimeout_ThrowsLockTimeoutException()
    {
        var dirPath = "Tests_" + Guid.NewGuid();
        Directory.CreateDirectory(dirPath);

        try
        {
            var db = new FileStorageEngine(dirPath, TimeSpan.FromMilliseconds(200));
            var lockFilePath = Path.Combine(dirPath, "stuck-async.j2k.lock");

            await using var externalHold = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            await Assert.ThrowsAsync<LockTimeoutException>(() =>
                db.InsertAsync("stuck-async", new TestClass { Code = "1", Name = "1" }));
        }
        finally
        {
            Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public void Insert_DifferentKeyThanExternallyHeldLock_SucceedsImmediately()
    {
        var dirPath = "Tests_" + Guid.NewGuid();
        Directory.CreateDirectory(dirPath);

        try
        {
            var db = new FileStorageEngine(dirPath, TimeSpan.FromMilliseconds(200));
            var lockFilePath = Path.Combine(dirPath, "held.j2k.lock");

            using var externalHold = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            // A different key must not be affected by the lock held on "held".
            db.Insert("unrelated", new TestClass { Code = "1", Name = "1" });
            var item = db.Get<TestClass>("unrelated");

            Assert.Equal("1", item.Code);
        }
        finally
        {
            Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public void PruneOrphanedFiles_RemovesLockFilesWithNoCorrespondingData()
    {
        using var block = new TestBlock();

        block.Db.Insert("alive", new TestClass { Code = "1", Name = "1" });

        // Simulate a stale lock file left behind by data removed via something other than this library.
        var orphanLockPath = Path.Combine(block.Db.DirectoryPath, "orphan.j2k.lock");
        File.WriteAllText(orphanLockPath, "");

        var aliveLockPath = Path.Combine(block.Db.DirectoryPath, "alive.j2k.lock");
        Assert.True(File.Exists(aliveLockPath));
        Assert.True(File.Exists(orphanLockPath));

        var (lockFilesRemoved, tempFilesRemoved) = block.Db.PruneOrphanedFiles();

        Assert.Equal(1, lockFilesRemoved);
        Assert.Equal(0, tempFilesRemoved);
        Assert.False(File.Exists(orphanLockPath));
        Assert.True(File.Exists(aliveLockPath));
    }

    [Fact]
    public void PruneOrphanedFiles_DoesNotRemoveAnActivelyHeldLockFile()
    {
        var dirPath = "Tests_" + Guid.NewGuid();
        Directory.CreateDirectory(dirPath);

        try
        {
            var db = new FileStorageEngine(dirPath);
            var lockFilePath = Path.Combine(dirPath, "held.j2k.lock");

            using var externalHold = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            // "held"'s .j2k doesn't exist, but its lock is actively held right now - must survive pruning.
            var (lockFilesRemoved, _) = db.PruneOrphanedFiles();

            Assert.Equal(0, lockFilesRemoved);
            Assert.True(File.Exists(lockFilePath));
        }
        finally
        {
            Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public void PruneOrphanedFiles_NoOrphans_ReturnsZero()
    {
        using var block = new TestBlock();

        block.Db.Insert("alive", new TestClass { Code = "1", Name = "1" });

        var (lockFilesRemoved, tempFilesRemoved) = block.Db.PruneOrphanedFiles();
        Assert.Equal(0, lockFilesRemoved);
        Assert.Equal(0, tempFilesRemoved);
    }

    [Fact]
    public async Task PruneOrphanedFiles_ConcurrentCalls_NeverDoubleCountTheSameOrphanLockFile()
    {
        // Acquire's FileMode.OpenOrCreate would transparently recreate a lock file that a racing
        // prune call just deleted, so a naive "acquire, check, delete" loop could have every
        // concurrent caller legitimately (but wrongly) conclude it removed a real orphan - the
        // later ones' "proof" would really just be the file they recreated moments earlier as a
        // side effect of checking. This must sum to at most 1 across all callers, every time.
        using var block = new TestBlock();
        var db = block.Db;
        var orphanLockPath = Path.Combine(db.DirectoryPath, "orphan.j2k.lock");
        await File.WriteAllTextAsync(orphanLockPath, "");

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var (lockFilesRemoved, _) = db.PruneOrphanedFiles();
            return lockFilesRemoved;
        })));

        Assert.Equal(1, results.Sum());
        Assert.False(File.Exists(orphanLockPath));
    }

    [Fact]
    public async Task PruneOrphanedFiles_ConcurrentCalls_NeverDoubleCountTheSameOrphanTempFile()
    {
        using var block = new TestBlock();
        var db = block.Db;
        var orphanTempPath = Path.Combine(db.DirectoryPath, "orphan.j2k.tmp");
        await File.WriteAllTextAsync(orphanTempPath, "");

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var (_, tempFilesRemoved) = db.PruneOrphanedFiles();
            return tempFilesRemoved;
        })));

        Assert.Equal(1, results.Sum());
        Assert.False(File.Exists(orphanTempPath));
    }

    [Fact]
    public void PruneOrphanedFiles_RemovesStaleTempFileWithNoActiveInsert()
    {
        using var block = new TestBlock();

        // Simulate a temp file left behind by an Insert that crashed between writing and renaming.
        var tempPath = Path.Combine(block.Db.DirectoryPath, "crashed.j2k.tmp");
        File.WriteAllText(tempPath, "partial-write-from-a-crash");

        var (lockFilesRemoved, tempFilesRemoved) = block.Db.PruneOrphanedFiles();

        Assert.Equal(0, lockFilesRemoved);
        Assert.Equal(1, tempFilesRemoved);
        Assert.False(File.Exists(tempPath));
    }

    [Fact]
    public void PruneOrphanedFiles_DoesNotRemoveATempFileForAKeyCurrentlyBeingInserted()
    {
        var dirPath = "Tests_" + Guid.NewGuid();
        Directory.CreateDirectory(dirPath);

        try
        {
            var db = new FileStorageEngine(dirPath);
            var fileName = Path.Combine(dirPath, "busy.j2k");
            var tempPath = fileName + ".tmp";

            File.WriteAllText(tempPath, "in-flight-write");

            // Hold this key's lock, simulating an Insert that's mid-write right now.
            using var externalHold = new FileStream(fileName + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            var (_, tempFilesRemoved) = db.PruneOrphanedFiles();

            Assert.Equal(0, tempFilesRemoved);
            Assert.True(File.Exists(tempPath));
        }
        finally
        {
            Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public async Task GetAllKeys_DuringConcurrentChurn_NeverThrows()
    {
        using var block = new TestBlock();

        for (var i = 0; i < 10; i++)
            await block.Db.InsertAsync($"seed-{i}", new TestClass { Code = i.ToString(), Name = "x" });
        
        var db = block.Db;

        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var churnTask = Task.Run(async () =>
        {
            var i = 0;
            while (!token.IsCancellationRequested)
            {
                var key = $"churn-{i++ % 5}";

                try
                {
                    await db.InsertAsync(key, new TestClass { Code = "x", Name = "x" }, token);
                    await db.DeleteAsync(key, token);
                }
                catch (StorageKeyNotFoundException)
                {
                    // another churn iteration or GetAllKeys' caller may have raced us here - fine
                }
                catch (OperationCanceledException)
                {
                    // Cancellation can arrive while a call is already mid-flight, past the
                    // while-check above - that's an expected way to stop, not a failure.
                    break;
                }
            }
        }, token);

        for (var i = 0; i < 50; i++)
        {
            var keys = (await block.Db.GetAllKeysAsync(token)).ToArray();
            Assert.True(keys.Length >= 10);
        }

        await cts.CancelAsync();
        await churnTask;
    }
}
