using Xunit;
using Isaac.FileStorage;

namespace FileStorage.UnitTest
{
    public class DeleteTests
    {
        [Fact]
        public void DeleteKey_KeyNotFound()
        {
            FileStorageEngine c = new FileStorageEngine("Test");
            string msg = string.Empty;

            try { c.Delete("inexistent_file"); }
            catch (System.Exception ex) { msg = ex.Message;}

            Assert.Equal("Key was not found.", msg);
        }

        [Fact]
        public void DeleteKey_EmptyKey()
        {
            FileStorageEngine c = new FileStorageEngine("Test");
            string msg = string.Empty;

            try { c.Delete(""); }
            catch (System.Exception ex) { msg = ex.Message; }

            Assert.Equal("Key cannot be empty.", msg);
        }

    }
}
