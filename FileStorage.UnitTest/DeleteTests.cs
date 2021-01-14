using Isaac.FileStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FileStorage.UnitTest
{
    public class DeleteTests
    {
        [Fact]
        public void DeleteKey_KeyNotFound()
        {
            Core c = new Core("Test");

            c.Delete("inexistent_file");
        }
    }
}
