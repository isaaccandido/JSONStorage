using System;
using System.IO;
using Xunit;
using Isaac.FileStorage.Lib;

namespace FileStorage.UnitTest;

public class DeleteTests
{
    [Fact]
    public void DeleteKey_KeyNotFound()
    {
        var c = new FileStorageEngine("Test");
        var msg = string.Empty;

        try
        {
            c.Delete("inexistent_file");
        }
        catch (System.Exception ex)
        {
            msg = ex.Message;
        }

        Assert.Equal("Key was not found.", msg);
    }

    [Fact]
    public void DeleteKey_EmptyKey()
    {
        FileStorageEngine c = new FileStorageEngine("Test");
        string msg = string.Empty;

        try { c.Delete(""); }
        catch (System.Exception ex) { msg = ex.Message; }

        Assert.Equal("Key cannot be empty.", msg);
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
        using var block = new TestBlock();

        block.Db.Insert("locked", new TestClass { Code = "1", Name = "1" });

        var filePath = Path.Combine(block.Db.DirectoryPath, "locked.j2k");
        using var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Delete("locked"));

        Assert.NotNull(ex.InnerException);
    }
}