using Isaac.FileStorage.Helpers;
using Xunit;

namespace FileStorage.UnitTest;

public class BsonHelperTests
{
    [Fact]
    public void Generate_NullObject_ReturnsEmptyByteArray()
    {
        var result = Bson.Generate<TestClass>(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Generate_ValidObject_ReturnsNonEmptyByteArray()
    {
        var result = Bson.Generate(new TestClass { Code = "1", Name = "1" });

        Assert.NotEmpty(result);
    }
}
