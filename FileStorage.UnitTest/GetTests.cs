using System;
using System.IO;
using System.Reflection;
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

            File.WriteAllText($"{Path.Combine(block.db.DirectoryPath,key)}.j2k", "bad_content");

            Exception ex = null;

            try
            {
                block.db.Get<TestClass>(key);
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

            block.db.Insert(key, new TestClass());

            Exception ex = null;

            try
            {
                block.db.Get<string>(key);
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

            string msg = $"Could not find file '{Path.Combine(path, block.db.DirectoryPath, key)}.j2k'.";

            Assert.Equal(msg, ex.Message);
        }
    }
}
