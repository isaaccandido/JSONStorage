using Isaac.FileStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace FileStorage.UnitTest
{
    public class GetAllKeysTests
    {
        [Theory]
        [InlineData("jfk.us.")]
        [InlineData("jfk.us.jk.")]
        [InlineData("potato~~")]
        [InlineData("jfk,us,jk,")]
        public void GetAllKeys_HasOneKey(string input)
        {
            using (var block = new TestBlock())
            {
                var test = new TestClass()
                {
                    Name = "John",
                    Code = "C001"
                };

                block.db.Insert(input, test);

                var allKeys = block.db.GetAllKeys()
                                      .ToArray();

                Assert.True(allKeys.Count() == 1);
            }
        }

        [Theory]
        [InlineData(12, "testFile")]
        [InlineData(127, "batata")]
        [InlineData(0, "batata")]
        public void GetAllKeys_HasManyKeys(int amount, string baseName)
        {
            using var block = new TestBlock();

            List<TestClass> lstTests = new();

            int code = 0;

            for (int i = 0; i < amount; i++)
            {
                var strCode = i.ToString().PadLeft(3, '0');
                var thisName = $"{baseName},.,.,.;{i}";

                lstTests.Add(new TestClass()
                {
                    Code = strCode,
                    Name = thisName
                });
            }

            foreach(var t in lstTests)
            {
                block.db.Insert(t.Name, t.Code);
            }

            var allKeys = block.db.GetAllKeys()
                                  .ToArray();

            Assert.True(allKeys.Count() == amount);
        }

        [Fact]
        public void GetAllKeys_NoKey()
        {
            using var block = new TestBlock();
            // get all keys when there's none
            var allKeys = block.db.GetAllKeys()
                                  .ToArray();
            Assert.True(allKeys.Count() == 0);
        }
    }
}
