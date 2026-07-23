using System;
using System.IO;
using Isaac.FileStorage;
using Xunit;

namespace FileStorage.UnitTest;

public class ConstructorTests
{
    [Fact]
    public void Constructor_NullDirectory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FileStorageEngine(null!));
    }

    [Fact]
    public void Constructor_EmptyDirectory_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new FileStorageEngine(string.Empty));
    }

    [Fact]
    public void Constructor_NegativeLockTimeout_ThrowsArgumentOutOfRangeException()
    {
        // No directory should be created either - validation happens before that.
        var dirPath = "Tests_" + Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() => new FileStorageEngine(dirPath, TimeSpan.FromSeconds(-1)));
        Assert.False(Directory.Exists(dirPath));
    }

    [Fact]
    public void Constructor_ExcessivelyLargeLockTimeout_ThrowsArgumentOutOfRangeException()
    {
        // TimeSpan.MaxValue would otherwise overflow DateTime.UtcNow + timeout deep inside KeyLock.
        var dirPath = "Tests_" + Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() => new FileStorageEngine(dirPath, TimeSpan.MaxValue));
        Assert.False(Directory.Exists(dirPath));
    }

    [Fact]
    public void Constructor_ZeroLockTimeout_IsAccepted()
    {
        var dirPath = "Tests_" + Guid.NewGuid();

        try
        {
            var db = new FileStorageEngine(dirPath, TimeSpan.Zero);
            db.Insert("k", new TestClass { Code = "1", Name = "1" });

            Assert.Equal("1", db.Get<TestClass>("k").Code);
        }
        finally
        {
            if (Directory.Exists(dirPath)) Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public void Constructor_CreatesDirectoryWhenMissing()
    {
        var dirPath = "Tests_" + Guid.NewGuid();
        Assert.False(Directory.Exists(dirPath));

        try
        {
            var db = new FileStorageEngine(dirPath);

            Assert.True(Directory.Exists(db.DirectoryPath));
        }
        finally
        {
            if (Directory.Exists(dirPath)) Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public void Constructor_UsesExistingDirectory()
    {
        var dirPath = "Tests_" + Guid.NewGuid();
        Directory.CreateDirectory(dirPath);

        try
        {
            var db = new FileStorageEngine(dirPath);
            db.Insert("k", new TestClass { Code = "1", Name = "1" });

            Assert.True(File.Exists(Path.Combine(db.DirectoryPath, "k.j2k")));
        }
        finally
        {
            Directory.Delete(dirPath, true);
        }
    }

    [Fact]
    public void Constructor_LegacyJkFiles_AreLeftUntouched()
    {
        // Legacy .jk -> .j2k migration and archiving were dropped in 1.6; a leftover
        // .jk file should be left exactly as-is, not converted, zipped, or exposed via GetAllKeys.
        var dirPath = "Tests_" + Guid.NewGuid();
        Directory.CreateDirectory(dirPath);

        try
        {
            var jkPath = Path.Combine(dirPath, "legacyKey.jk");
            File.WriteAllText(jkPath, "{\"Code\":\"1\",\"Name\":\"1\"}");

            var db = new FileStorageEngine(dirPath);

            Assert.True(File.Exists(jkPath));
            Assert.False(File.Exists(Path.Combine(dirPath, "legacyKey.j2k")));
            Assert.False(File.Exists(Path.Combine(dirPath, "legacyFiles.zip")));
            Assert.Empty(db.GetAllKeys());
        }
        finally
        {
            Directory.Delete(dirPath, true);
        }
    }
}
