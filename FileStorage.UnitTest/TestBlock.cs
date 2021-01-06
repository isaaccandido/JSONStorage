using Isaac.FileStorage;
using System;
using System.IO;

namespace FileStorage.UnitTest
{
    public class TestBlock : IDisposable
    {
        public Core db { get; }

        public TestBlock()
        {
            db = new Core("unitTestTests");
        }

        public void Dispose()
        {
            Directory.Delete(db.DirectoryPath, true);
        }
    }
}
