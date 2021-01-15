using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FileStorage.UnitTest
{
    public class bsonGeneratorTests
    {
        [Fact]
        public static void bsonGenerator_NullValue()
        {
            using var block = new TestBlock();
            block.db.Insert<Transaction>("1234", null);
            var nullClass = block.db.Get<Transaction>("1234");

            Assert.Null(nullClass);
        }
    }
}
