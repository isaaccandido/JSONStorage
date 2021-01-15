using Isaac.FileStorage;
using Xunit;

namespace FileStorage.UnitTest
{
    public class DeleteTests
    {
        [Fact]
        public void DeleteKey_KeyNotFound()
        {
            Core c = new Core("Test");
            string msg = string.Empty;

            try { c.Delete("inexistent_file"); }
            catch (System.Exception ex) { msg = ex.Message;}

            Assert.Equal("Key was not found.", msg);
        }

        [Fact]
        public void DeleteKey_EmptyKey()
        {
            Core c = new Core("Test");
            string msg = string.Empty;

            try { c.Delete(""); }
            catch (System.Exception ex) { msg = ex.Message; }

            Assert.Equal("Key cannot be empty.", msg);
        }

    }
}
