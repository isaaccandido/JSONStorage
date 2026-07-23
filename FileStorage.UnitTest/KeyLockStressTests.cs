using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Isaac.FileStorage.Concurrency;
using Xunit;

namespace FileStorage.UnitTest;

public class KeyLockStressTests
{
    [Fact(Timeout = 30000)]
    public async Task Acquire_HeavyContentionOnSameKey_NoExceptionsAndCleanFinalState()
    {
        var fileName = "stress-same-key-" + Guid.NewGuid();

        var tasks = Enumerable.Range(0, 500)
            .Select(async _ =>
            {
                using var handle = await KeyLock.AcquireAsync(
                    fileName,
                    TimeSpan.FromSeconds(20),
                    CancellationToken.None
                );

                await Task.Yield();
            });

        await Task.WhenAll(tasks);

        Assert.False(KeyLock.IsTracked(fileName));
    }

    [Fact(Timeout = 30000)]
    public async Task Acquire_HeavyContentionAcrossManyKeys_NoExceptionsAndCleanFinalState()
    {
        var keys = Enumerable.Range(0, 50)
            .Select(i => "stress-many-keys-" + i + "-" + Guid.NewGuid())
            .ToArray();

        var tasks = keys.SelectMany(key => Enumerable.Range(0, 20)
            .Select(async _ =>
            {
                using var handle = await KeyLock.AcquireAsync(
                    key,
                    TimeSpan.FromSeconds(20),
                    CancellationToken.None
                );

                await Task.Yield();
            }));

        await Task.WhenAll(tasks);

        Assert.All(keys, key => Assert.False(KeyLock.IsTracked(key)));
    }

    [Fact(Timeout = 30000)]
    public void Acquire_HeavyContentionMixingSyncAndAsync_NoExceptionsAndCleanFinalState()
    {
        var fileName = "stress-mixed-" + Guid.NewGuid();

        var tasks = Enumerable.Range(0, 300)
            .Select(i => Task.Run(async () =>
            {
                if (i % 2 == 0)
                {
                    using var handle = KeyLock.Acquire(fileName, TimeSpan.FromSeconds(20));
                }
                else
                {
                    using var handle =
                        await KeyLock.AcquireAsync(fileName, TimeSpan.FromSeconds(20), CancellationToken.None);
                }
            })).ToArray();

        Task.WaitAll(tasks);

        Assert.False(KeyLock.IsTracked(fileName));
    }
}