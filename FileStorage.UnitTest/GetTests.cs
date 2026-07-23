using System;
using System.IO;
using Isaac.FileStorage.CustomExceptions;
using Xunit;

namespace FileStorage.UnitTest;

public class GetTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("jfk.us.jk")]
    [InlineData("jfk.us.jk.")]
    [InlineData("jfk,us,jk,")]
    public void Get_KeyExists(string key)
    {
        using var block = new TestBlock();

        var originItem = new TestClass
        {
            Code = key,
            Name = key
        };

        block.Db.Insert(key, originItem);

        var retrievedItem = block.Db.Get<TestClass>(key);

        Assert.True(retrievedItem != null);
        Assert.Equal(key, retrievedItem.Code);
        Assert.Equal(key, retrievedItem.Name);
    }

    [Fact]
    public void Get_EmptyKey()
    {
        using var block = new TestBlock();

        Assert.Throws<EmptyKeyException>(() => block.Db.Get<TestClass>(string.Empty));
    }

    [Fact]
    public void Get_NullKey()
    {
        using var block = new TestBlock();

        Assert.Throws<EmptyKeyException>(() => block.Db.Get<TestClass>(null!));
    }

    [Fact]
    public void Get_KeyExistsButFileCorrupt()
    {
        using var block = new TestBlock();

        const string key = "badFile";

        File.WriteAllText($"{Path.Combine(block.Db.DirectoryPath,key)}.j2k", "bad_content");

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Get<TestClass>(key));

        var msg = $"Cannot get data content from file of key '{key}'. " +
                     "This happened because either the file is unreadable or the generic type mismatches. " +
                     $"The current destination type is '{typeof(TestClass)}' but I'm unable to determine the actual type. " +
                     "Try verifying the type you are trying to recover data to and try again.";

        Assert.Equal(msg, ex.Message);
    }

    [Fact]
    public void Get_KeyExistsButWrongType()
    {
        using var block = new TestBlock();

        const string key = "test";

        block.Db.Insert(key, new TestClass());

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Get<string>(key));

        var msg = $"Cannot get data content from file of key '{key}'. " +
                     "This happened because either the file is unreadable or the generic type mismatches. " +
                     $"The current destination type is '{typeof(string)}' but I'm unable to determine the actual type. " +
                     "Try verifying the type you are trying to recover data to and try again.";

        Assert.Equal(msg, ex.Message);
    }

    [Fact]
    public void Get_InexistentKey()
    {
        using var block = new TestBlock();

        const string key = "inexistentKey";

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Get<TestClass>(key));

        var msg = $"Cannot get data content from file of key '{key}'. " +
                     "This happened because either the file is unreadable or the generic type mismatches. " +
                     $"The current destination type is '{typeof(TestClass)}' but I'm unable to determine the actual type. " +
                     "Try verifying the type you are trying to recover data to and try again.";

        Assert.Equal(msg, ex.Message);
        Assert.IsType<FileNotFoundException>(ex.InnerException);
    }

    [Fact]
    public void Get_InexistentKey_LeavesNoLockFileBehind()
    {
        // Acquiring the lock to safely check for the key's existence used to leave a stray
        // .lock file behind forever for a key that never had any data - a very common pattern
        // ("check if this key exists") should leave the directory exactly as it found it.
        using var block = new TestBlock();

        const string key = "inexistentKeyNoLock";

        Assert.Throws<InvalidOperationException>(() => block.Db.Get<TestClass>(key));

        Assert.False(File.Exists(Path.Combine(block.Db.DirectoryPath, $"{key}.j2k.lock")));
    }

    [Fact]
    public void Get_KeyExistsButFileCorrupt_PreservesInnerException()
    {
        using var block = new TestBlock();

        const string key = "badFile";

        File.WriteAllText($"{Path.Combine(block.Db.DirectoryPath, key)}.j2k", "bad_content");

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Get<TestClass>(key));

        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Get_KeyExistsButFileCorrupt_StillLeavesLockFileForReuse()
    {
        // Unlike a genuinely missing key, a key with real (if corrupt) data is a real key -
        // its lock file must survive, since future access to this same key still needs it.
        using var block = new TestBlock();

        const string key = "badFileKeepsLock";

        File.WriteAllText($"{Path.Combine(block.Db.DirectoryPath, key)}.j2k", "bad_content");

        Assert.Throws<InvalidOperationException>(() => block.Db.Get<TestClass>(key));

        Assert.True(File.Exists(Path.Combine(block.Db.DirectoryPath, $"{key}.j2k.lock")));
    }
}
