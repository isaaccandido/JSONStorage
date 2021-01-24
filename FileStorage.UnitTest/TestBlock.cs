using Isaac.FileStorage;
using System;
using System.IO;

namespace FileStorage.UnitTest
{
    public class TestBlock : IDisposable
    {
        public FileStorageEngine db { get; }
        
        public TestBlock()
        {
            var newPath = "Tests_" + Guid.NewGuid().ToString();

            db = new FileStorageEngine(newPath);
        }

        public void Dispose()
        {
            Directory.Delete(db.DirectoryPath, true);
        }
    }
}
