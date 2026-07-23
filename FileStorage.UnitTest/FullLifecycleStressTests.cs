using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FileStorage.UnitTest;

public class FullLifecycleStressTests
{
    [Fact(Timeout = 60000)]
    public async Task MixedInsertGetDeletePruneAcrossManyKeys_NeverCorruptsAndEndsClean()
    {
        using var block = new TestBlock();
        var db = block.Db;

        var keys = Enumerable.Range(0, 15).Select(i => "lifecycle-" + i).ToArray();

        var workers = Enumerable.Range(0, 8).Select(workerId => Task.Run(async () =>
        {
            var localRandom = new Random(12345 + workerId);

            for (var iteration = 0; iteration < 40; iteration++)
            {
                var key = keys[localRandom.Next(keys.Length)];
                var action = localRandom.Next(5);

                try
                {
                    switch (action)
                    {
                        case 0:
                            await db.InsertAsync(key, new TestClass { Code = workerId.ToString(), Name = "ok" });
                            break;
                        case 1:
                            var circular = new SelfReferencingClass();
                            circular.Self = circular;
                            await db.InsertAsync(key, circular);
                            break;
                        case 2:
                            await db.GetAsync<TestClass>(key);
                            break;
                        case 3:
                            await db.DeleteAsync(key);
                            break;
                        case 4:
                            db.PruneOrphanedFiles();
                            break;
                    }
                }
                catch (Exception)
                {
                    // All of these are expected outcomes under this much contention/failure
                    // injection: missing key, unserializable object, lock timeout, etc.
                }
            }
        })).ToArray();

        await Task.WhenAll(workers);

        // Whatever the final state, it must be internally consistent: every key that still has
        // data must be readable, and GetAllKeys must never throw or disagree with what's on disk.
        var survivingKeys = (await db.GetAllKeysAsync()).ToArray();
        foreach (var key in survivingKeys)
        {
            var item = db.Get<TestClass>(key);
            Assert.NotNull(item);
        }

        // Final cleanup pass: every key touched during the run should now be fully prunable -
        // nothing left in an inconsistent, half-acquired state.
        for (var i = 0; i < 3; i++) db.PruneOrphanedFiles();

        foreach (var key in keys)
        {
            var lockPath = Path.Combine(db.DirectoryPath, $"{key}.j2k.lock");
            var dataPath = Path.Combine(db.DirectoryPath, $"{key}.j2k");
            var tempPath = Path.Combine(db.DirectoryPath, $"{key}.j2k.tmp");

            // A lock file may legitimately persist only if the key still has real data.
            if (File.Exists(lockPath))
            {
                Assert.True(File.Exists(dataPath),
                    $"Key '{key}' has a lock file but no data - should have been pruned or never created.");
            }

            Assert.False(File.Exists(tempPath), $"Key '{key}' still has a temp file after repeated pruning.");
        }
    }
}
