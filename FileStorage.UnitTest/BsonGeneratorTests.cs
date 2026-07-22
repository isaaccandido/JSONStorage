using Xunit;

namespace FileStorage.UnitTest;

public class BsonGeneratorTests
{
    [Fact]
    public static void bsonGenerator_NullValue()
    {
        using var block = new TestBlock();
        block.Db.Insert<Transaction>("1234", null);
        var nullClass = block.Db.Get<Transaction>("1234");

        Assert.Null(nullClass);
    }
}