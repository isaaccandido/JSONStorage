using System;
using System.IO;
using Xunit;
using Isaac.FileStorage.CustomExceptions;

namespace FileStorage.UnitTest;

public class DeleteTests
{
    [Fact]
    public void DeleteKey_KeyNotFound()
    {
        using var block = new TestBlock();

        var ex = Assert.Throws<StorageKeyNotFoundException>(() => block.Db.Delete("inexistent_file"));

        Assert.Equal("Key was not found.", ex.Message);
    }

    [Fact]
    public void DeleteKey_KeyNotFound_LeavesNoLockFileBehind()
    {
        // Acquiring the lock to safely check for the key's existence used to leave a stray
        // .lock file behind forever for a key that never had any data.
        using var block = new TestBlock();

        Assert.Throws<StorageKeyNotFoundException>(() => block.Db.Delete("inexistent_no_lock"));

        Assert.False(File.Exists(Path.Combine(block.Db.DirectoryPath, "inexistent_no_lock.j2k.lock")));
    }

    [Fact]
    public void DeleteKey_EmptyKey()
    {
        using var block = new TestBlock();

        var ex = Assert.Throws<EmptyKeyException>(() => block.Db.Delete(""));

        Assert.Equal("Key cannot be empty.", ex.Message);
    }

    [Fact]
    public void DeleteKey_RemovesFile()
    {
        using var block = new TestBlock();

        block.Db.Insert("toDelete", new TestClass { Code = "1", Name = "1" });

        block.Db.Delete("toDelete");

        Assert.Empty(block.Db.GetAllKeys());
    }

    [Fact]
    public void DeleteKey_FileLocked_ThrowsInvalidOperationExceptionWithInnerException()
    {
        // Share-mode locks that block deletion of an open file are a Windows-only concept;
        // POSIX systems (Linux/macOS) allow unlinking a file that's still open elsewhere.
        if (!OperatingSystem.IsWindows()) return;

        using var block = new TestBlock();

        block.Db.Insert("locked", new TestClass { Code = "1", Name = "1" });

        var filePath = Path.Combine(block.Db.DirectoryPath, "locked.j2k");
        using var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Delete("locked"));

        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void DeleteKey_FileLocked_StillLeavesLockFileForReuse()
    {
        // Unlike a genuinely missing key, a key whose delete failed because the file is
        // otherwise in use is a real key with real data still on disk - its lock file must
        // survive for future access to that same key.
        if (!OperatingSystem.IsWindows()) return;

        using var block = new TestBlock();

        block.Db.Insert("lockedKeepsLock", new TestClass { Code = "1", Name = "1" });

        var filePath = Path.Combine(block.Db.DirectoryPath, "lockedKeepsLock.j2k");
        using var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Throws<InvalidOperationException>(() => block.Db.Delete("lockedKeepsLock"));

        Assert.True(File.Exists(Path.Combine(block.Db.DirectoryPath, "lockedKeepsLock.j2k.lock")));
    }
}