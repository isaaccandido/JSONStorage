using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace FileStorage.UnitTest;

public class GetAllKeysTests
{
    [Theory]
    [InlineData("jfk.us.")]
    [InlineData("jfk.us.jk.")]
    [InlineData("jfk,us,jk,")]
    public void GetAllKeys_AirportBug(string input)
    {
        using var block = new TestBlock();
        var test = new TestClass()
        {
            Name = "John",
            Code = "C001"
        };

        block.Db.Insert(input, test);

        var allKeys = block.Db.GetAllKeys()
            .ToArray();

        Assert.Single(allKeys);
    }

    [Theory]
    [InlineData(12, "testFile")]
    [InlineData(127, "batata")]
    [InlineData(0, "batata")]
    public void GetAllKeys_HasManyKeys(int amount, string baseName)
    {
        using var block = new TestBlock();

        List<TestClass> lstTests = new();

        for (int i = 0; i < amount; i++)
        {
            var strCode = i.ToString("000");
            var thisName = $"{baseName},.,.,.;{i}";

            lstTests.Add(new TestClass()
            {
                Code = strCode,
                Name = thisName
            });
        }

        foreach (var t in lstTests)
        {
            block.Db.Insert(t.Name, t);
        }

        var allKeys = block.Db.GetAllKeys().ToArray();

        Assert.Equal(amount, allKeys.Length);
    }

    [Fact]
    public void GetAllKeys_NoKey()
    {
        using var block = new TestBlock();
        // get all keys when there's none
        var allKeys = block.Db.GetAllKeys()
            .ToArray();
        Assert.Empty(allKeys);
    }

    [Fact]
    public void GetAllKeys_IgnoresNonJ2KFiles()
    {
        using var block = new TestBlock();

        block.Db.Insert("realKey", new TestClass { Code = "1", Name = "1" });
        File.WriteAllText(Path.Combine(block.Db.DirectoryPath, "notes.txt"), "irrelevant");
        File.WriteAllText(Path.Combine(block.Db.DirectoryPath, "legacyFiles.zip"), "fake zip content");

        var allKeys = block.Db.GetAllKeys().ToArray();

        Assert.Single(allKeys);
        Assert.Equal("realKey", allKeys[0]);
    }
}