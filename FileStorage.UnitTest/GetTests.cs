using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FileStorage.UnitTest
{
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

            block.db.Insert(key, originItem);

            var retrievedItem = block.db.Get<TestClass>(key);

            Assert.True(retrievedItem != null);
        }

        [Fact]
        public static void Get_KeyExistsButFileCorrupt()
        {
            using var block = new TestBlock();

            string key = "badFile";

            File.WriteAllText($"{Path.Combine(block.db.DirectoryPath,key)}.jk", "bad_content");

            Exception ex = null;

            try
            {
                block.db.Get<TestClass>(key);
            }
            catch (Exception exception)
            {
                ex = exception;
            }

            string msg = $"Unexpected character encountered while parsing value: b. Path '', line 0, position 0.";

            Assert.Equal(msg, ex.Message);
        }

        [Fact]
        public static void Get_InexistentKey()
        {
            using var block = new TestBlock();
            
            string key = "inexistingKey";

            Exception ex = null;

            try
            {
                block.db.Get<TestClass>(key);
            }
            catch (Exception exception)
            {
                ex = exception;
            }


            string path = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.FullName;

            string msg = $"Could not find file '{Path.Combine(path, block.db.DirectoryPath, key)}.jk'.";

            Assert.Equal(msg, ex.Message);
        }
    }
}
