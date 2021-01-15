using Isaac.FileStorage;
using System;
using Xunit;

namespace FileStorage.UnitTest
{
    public class InsertTests
    {
        [Theory]
        [InlineData("bla")]
        [InlineData("batata")]
        public static void Insert_One(string key)
        {
            using var block = new TestBlock();

            block.db.Insert(key, new TestClass()
            {
                Code = "001",
                Name = "001"
            });
            
            var item = block.db.Get<TestClass>(key);

            Assert.NotNull(item);
        }

        [Fact]
        public static void Insert_EmptyKey()
        {
            using var block = new TestBlock();

            Assert.Throws<EmptyKeyException>(() =>
            {
                block.db.Insert(string.Empty, new TestClass()
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
                block.db.Insert(null, new TestClass()
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

            block.db.Insert(".", new TestClass()
            {
                Code = "001",
                Name = "001"
            });
        }
        [Fact]
        public static void Insert_InvalidPathTilde()
        {
            using var block = new TestBlock();

            block.db.Insert("potato~~", new TestClass()
            {
                Code = "001",
                Name = "001"
            });
        }
    }
}
