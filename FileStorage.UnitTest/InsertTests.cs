using System;
using System.IO;
using Isaac.FileStorage.CustomExceptions;
using Xunit;

namespace FileStorage.UnitTest;

public class InsertTests
{
    [Theory]
    [InlineData("bla")]
    [InlineData("batata")]
    public void Insert_One(string key)
    {
        using var block = new TestBlock();

        block.Db.Insert(key, new TestClass
        {
            Code = "001",
            Name = "001"
        });

        var item = block.Db.Get<TestClass>(key);

        Assert.NotNull(item);
        Assert.Equal("001", item.Code);
        Assert.Equal("001", item.Name);
    }

    [Fact]
    public void Insert_OverwritesExistingKey()
    {
        using var block = new TestBlock();

        block.Db.Insert("dup", new TestClass { Code = "first", Name = "first" });
        block.Db.Insert("dup", new TestClass { Code = "second", Name = "second" });

        var item = block.Db.Get<TestClass>("dup");

        Assert.Equal("second", item.Code);
        Assert.Equal("second", item.Name);
    }

    [Fact]
    public void Insert_UnserializableObject_ThrowsInvalidOperationExceptionWithInnerException()
    {
        using var block = new TestBlock();

        var circular = new SelfReferencingClass();
        circular.Self = circular;

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Insert("circular", circular));

        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Insert_FailedWrite_LeavesExistingDataIntact()
    {
        // Insert writes to a temp file and atomically renames it over the destination, so a
        // failed write (here: unserializable object) must never touch - let alone corrupt or
        // truncate - the key's existing, still-valid data.
        using var block = new TestBlock();

        block.Db.Insert("atomic-key", new TestClass { Code = "original", Name = "original" });

        var circular = new SelfReferencingClass();
        circular.Self = circular;

        Assert.Throws<InvalidOperationException>(() => block.Db.Insert("atomic-key", circular));

        var item = block.Db.Get<TestClass>("atomic-key");
        Assert.Equal("original", item.Code);
        Assert.Equal("original", item.Name);
    }

    [Fact]
    public void Insert_DoesNotLeaveTempFileBehindOnSuccess()
    {
        using var block = new TestBlock();

        block.Db.Insert("clean-key", new TestClass { Code = "1", Name = "1" });

        Assert.False(File.Exists(Path.Combine(block.Db.DirectoryPath, "clean-key.j2k.tmp")));
    }

    [Fact]
    public void Insert_FailedWrite_CleansUpTempFile()
    {
        using var block = new TestBlock();

        var circular = new SelfReferencingClass();
        circular.Self = circular;

        Assert.Throws<InvalidOperationException>(() => block.Db.Insert("failed-key", circular));

        Assert.False(File.Exists(Path.Combine(block.Db.DirectoryPath, "failed-key.j2k.tmp")));
    }

    [Fact]
    public void Insert_FailedWriteOnBrandNewKey_LeavesNoLockFileBehind()
    {
        // Acquiring the lock to attempt the write creates the lock file as a side effect. If the
        // insert fails and the key still has no real data (never succeeded before, or now),
        // nothing should be left behind - not even that lock file.
        using var block = new TestBlock();

        var circular = new SelfReferencingClass();
        circular.Self = circular;

        Assert.Throws<InvalidOperationException>(() => block.Db.Insert("brand-new-no-lock", circular));

        Assert.Empty(Directory.GetFiles(block.Db.DirectoryPath));
    }

    [Fact]
    public void Insert_FailedWriteOnKeyWithExistingData_StillLeavesLockFileForReuse()
    {
        // Unlike a brand-new key, a key that already has real data from an earlier successful
        // insert is a real key - its lock file must survive a later failed insert attempt.
        using var block = new TestBlock();

        block.Db.Insert("existing-keeps-lock", new TestClass { Code = "original", Name = "original" });

        var circular = new SelfReferencingClass();
        circular.Self = circular;

        Assert.Throws<InvalidOperationException>(() => block.Db.Insert("existing-keeps-lock", circular));

        Assert.True(File.Exists(Path.Combine(block.Db.DirectoryPath, "existing-keeps-lock.j2k.lock")));

        var item = block.Db.Get<TestClass>("existing-keeps-lock");
        Assert.Equal("original", item.Code);
    }

    [Fact]
    public void Insert_EmptyKey()
    {
        using var block = new TestBlock();

        Assert.Throws<EmptyKeyException>(() =>
        {
            block.Db.Insert(string.Empty, new TestClass
            {
                Code = "001",
                Name = "001"
            });
        });
    }
    [Fact]
    public void Insert_NullKey()
    {
        using var block = new TestBlock();

        Assert.Throws<EmptyKeyException>(() =>
        {
            block.Db.Insert(null!, new TestClass
            {
                Code = "001",
                Name = "001"
            });
        });
    }
    [Fact]
    public void Insert_DotDot()
    {
        using var block = new TestBlock();

        block.Db.Insert(".", new TestClass
        {
            Code = "001",
            Name = "001"
        });
    }
    [Fact]
    public void Insert_InvalidPathTilde()
    {
        using var block = new TestBlock();

        block.Db.Insert("potato~~", new TestClass
        {
            Code = "001",
            Name = "001"
        });
    }
}
