using System;
using System.IO;
using Isaac.FileStorage.Lib.CustomExceptions;
using Xunit;

namespace FileStorage.UnitTest;

public class InvalidKeyTests
{
    [Theory]
    [InlineData("../evil")]
    [InlineData("../../evil")]
    [InlineData("..\\evil")]
    [InlineData("subdir/../../evil")]
    public void Insert_PathTraversalKey_ThrowsInvalidKeyException(string key)
    {
        using var block = new TestBlock();

        Assert.Throws<InvalidKeyException>(() =>
            block.Db.Insert(key, new TestClass { Code = "1", Name = "1" }));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("../../evil")]
    public void Get_PathTraversalKey_ThrowsInvalidKeyException(string key)
    {
        using var block = new TestBlock();

        Assert.Throws<InvalidKeyException>(() => block.Db.Get<TestClass>(key));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("../../evil")]
    public void Delete_PathTraversalKey_ThrowsInvalidKeyException(string key)
    {
        using var block = new TestBlock();

        Assert.Throws<InvalidKeyException>(() => block.Db.Delete(key));
    }

    [Fact]
    public void Insert_AbsolutePathKey_ThrowsInvalidKeyException()
    {
        using var block = new TestBlock();
        var outsidePath = Path.Combine(Path.GetTempPath(), "jsonstorage_outside_" + Guid.NewGuid());

        Assert.Throws<InvalidKeyException>(() =>
            block.Db.Insert(outsidePath, new TestClass { Code = "1", Name = "1" }));
    }

    [Fact]
    public void Insert_PathTraversalKey_DoesNotWriteFileOutsideDirectory()
    {
        using var block = new TestBlock();
        var outsideDir = Path.Combine(Path.GetTempPath(), "jsonstorage_outside_" + Guid.NewGuid());
        Directory.CreateDirectory(outsideDir);

        try
        {
            var maliciousKey = Path.Combine(outsideDir, "evil");

            Assert.Throws<InvalidKeyException>(() =>
                block.Db.Insert(maliciousKey, new TestClass { Code = "1", Name = "1" }));

            Assert.Empty(Directory.GetFiles(outsideDir));
        }
        finally
        {
            Directory.Delete(outsideDir, true);
        }
    }

    [Fact]
    public void Insert_KeyEscapingViaSiblingDirectoryPrefix_ThrowsInvalidKeyException()
    {
        using var block = new TestBlock();

        var siblingKey = $"..{Path.DirectorySeparatorChar}" +
                          $"{new DirectoryInfo(block.Db.DirectoryPath).Name}Evil{Path.DirectorySeparatorChar}file";

        Assert.Throws<InvalidKeyException>(() =>
            block.Db.Insert(siblingKey, new TestClass { Code = "1", Name = "1" }));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..job")]
    [InlineData("...")]
    public void Insert_KeyStartingWithDotsButNotATraversal_Succeeds(string key)
    {
        // A filename that merely starts with ".." (e.g. the "." key becomes "..j2k" once the
        // extension is appended) is not a path-traversal segment and must be allowed.
        using var block = new TestBlock();

        block.Db.Insert(key, new TestClass { Code = "1", Name = "1" });

        var item = block.Db.Get<TestClass>(key);
        Assert.Equal("1", item.Code);
    }

    [Fact]
    public void Insert_TraversalThatResolvesInsideDirectory_Succeeds()
    {
        using var block = new TestBlock();

        block.Db.Insert("sub/../safe", new TestClass { Code = "1", Name = "1" });

        var item = block.Db.Get<TestClass>("safe");

        Assert.Equal("1", item.Code);
    }
}
