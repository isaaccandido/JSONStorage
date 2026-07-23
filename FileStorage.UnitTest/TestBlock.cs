using System;
using System.IO;
using Isaac.FileStorage;

namespace FileStorage.UnitTest;

public class TestBlock : IDisposable
{
    public FileStorageEngine Db { get; }
        
    public TestBlock()
    {
        var newPath = "Tests_" + Guid.NewGuid();

        Db = new FileStorageEngine(newPath);
    }

    public void Dispose()
    {
        Directory.Delete(Db.DirectoryPath, true);
        GC.SuppressFinalize(this);
    }
}