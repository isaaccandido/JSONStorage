using Isaac.FileStorage;
using System;
using System.IO;
using System.Threading;

namespace FileStorage.UnitTest
{
    public class TestBlock : IDisposable
    {
        static object lockObj;
        static TestBlock()
        {
            lockObj = new object();
        }

        public Core db { get; }
        
        public TestBlock()
        {
            db = new Core("unitTestTests");
            nukeDB();
        }

        public void Dispose()
        {
            nukeDB();
        }

        void nukeDB()
        {
            // I know this is bad
            // I know I should not have done this
            // But I am desperate at this point
            // nothing else worked
            lock (lockObj)
            {
                for (int retries = 0; retries < 5; retries++)
                {
                    try
                    {
                        foreach (var f in Directory.GetFiles(db.DirectoryPath))
                        {
                            File.Delete(f);
                        }
                    }
                    catch { Thread.Sleep(50); }
                }
            }
        }
    }
}
