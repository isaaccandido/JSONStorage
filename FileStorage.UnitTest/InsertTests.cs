using System;
using Isaac.FileStorage.CustomExceptions;
using Xunit;

namespace FileStorage.UnitTest;

public class InsertTests
{
    [Theory]
    [InlineData("bla")]
    [InlineData("batata")]
    public static void Insert_One(string key)
    {
        using var block = new TestBlock();

        block.Db.Insert(key, new TestClass()
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
    public static void Insert_OverwritesExistingKey()
    {
        using var block = new TestBlock();

        block.Db.Insert("dup", new TestClass { Code = "first", Name = "first" });
        block.Db.Insert("dup", new TestClass { Code = "second", Name = "second" });

        var item = block.Db.Get<TestClass>("dup");

        Assert.Equal("second", item.Code);
        Assert.Equal("second", item.Name);
    }

    [Fact]
    public static void Insert_UnserializableObject_ThrowsInvalidOperationExceptionWithInnerException()
    {
        using var block = new TestBlock();

        var circular = new SelfReferencingClass();
        circular.Self = circular;

        var ex = Assert.Throws<InvalidOperationException>(() => block.Db.Insert("circular", circular));

        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public static void Insert_EmptyKey()
    {
        using var block = new TestBlock();

        Assert.Throws<EmptyKeyException>(() =>
        {
            block.Db.Insert(string.Empty, new TestClass()
            {
                Code = "001",
                Name = "001"
            });
        });
    }
    [Fact]
    public static void Insert_NullKey()
    {
        using var block = new TestBlock();

        Assert.Throws<EmptyKeyException>(() =>
        {
            block.Db.Insert(null, new TestClass()
            {
                Code = "001",
                Name = "001"
            });
        });
    }
    [Fact]
    public static void Insert_DotDot()
    {
        using var block = new TestBlock();

        block.Db.Insert(".", new TestClass()
        {
            Code = "001",
            Name = "001"
        });
    }
    [Fact]
    public static void Insert_InvalidPathTilde()
    {
        using var block = new TestBlock();

        block.Db.Insert("potato~~", new TestClass()
        {
            Code = "001",
            Name = "001"
        });
    }
}