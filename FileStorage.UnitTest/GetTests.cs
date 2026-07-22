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
    public static void Get_KeyExists(string key)
    {
        using var block = new TestBlock();

        var originItem = new TestClass()
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
    public static void Get_EmptyKey()
    {
        using var block = new TestBlock();

        Assert.Throws<EmptyKeyException>(() => block.Db.Get<TestClass>(string.Empty));
    }

    [Fact]
    public static void Get_NullKey()
    {
        using var block = new TestBlock();

        Assert.Throws<EmptyKeyException>(() => block.Db.Get<TestClass>(null));
    }

    [Fact]
    public static void Get_KeyExistsButFileCorrupt()
    {
        using var block = new TestBlock();

        string key = "badFile";

        File.WriteAllText($"{Path.Combine(block.Db.DirectoryPath,key)}.j2k", "bad_content");

        Exception ex = null;

        try
        {
            block.Db.Get<TestClass>(key);
        }
        catch (Exception exception)
        {
            ex = exception;
        }

        string msg = $"Cannot get data content from file of key '{key}'. " +
                     "This happened because either the file is unreadable or the generic type mismatches. " +
                     $"The current destination type is '{typeof(TestClass)}' but I'm unable to determine the actual type. " +
                     "Try verifying the type you are trying to recover data to and try again.";

        Assert.Equal(msg, ex.Message);
    }

    [Fact]
    public static void Get_KeyExistsButWrongType()
    {
        using var block = new TestBlock();

        string key = "test";

        block.Db.Insert(key, new TestClass());

        Exception ex = null;

        try
        {
            block.Db.Get<string>(key);
        }
        catch (Exception exception)
        {
            ex = exception;
        }

        string msg = $"Cannot get data content from file of key '{key}'. " +
                     "This happened because either the file is unreadable or the generic type mismatches. " +
                     $"The current destination type is '{typeof(string)}' but I'm unable to determine the actual type. " +
                     "Try verifying the type you are trying to recover data to and try again.";

        Assert.Equal(msg, ex.Message);
    }

    [Fact]
    public static void Get_InexistentKey()
    {
        using var block = new TestBlock();

        string key = "inexistingKey";

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Get<TestClass>(key));

        string msg = $"Cannot get data content from file of key '{key}'. " +
                     "This happened because either the file is unreadable or the generic type mismatches. " +
                     $"The current destination type is '{typeof(TestClass)}' but I'm unable to determine the actual type. " +
                     "Try verifying the type you are trying to recover data to and try again.";

        Assert.Equal(msg, ex.Message);
        Assert.IsType<FileNotFoundException>(ex.InnerException);
    }

    [Fact]
    public static void Get_KeyExistsButFileCorrupt_PreservesInnerException()
    {
        using var block = new TestBlock();

        string key = "badFile";

        File.WriteAllText($"{Path.Combine(block.Db.DirectoryPath, key)}.j2k", "bad_content");

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Get<TestClass>(key));

        Assert.NotNull(ex.InnerException);
    }
}