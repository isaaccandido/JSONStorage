using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Isaac.FileStorage.Concurrency;
using Isaac.FileStorage.CustomExceptions;
using Xunit;

namespace FileStorage.UnitTest;

public class KeyLockTests
{
    [Fact(Timeout = 10000)]
    public async Task Acquire_SameKeyHeldInProcess_RespectsConfiguredTimeout()
    {
        var fileName = "keylock-test-" + Guid.NewGuid();

        using var held = KeyLock.Acquire(fileName, TimeSpan.FromSeconds(30));

        var stopwatch = Stopwatch.StartNew();
        var ex = await Record.ExceptionAsync(() => Task.Run(() =>
        {
            using var _ = KeyLock.Acquire(fileName, TimeSpan.FromMilliseconds(300));
        }));
        stopwatch.Stop();

        Assert.IsType<LockTimeoutException>(ex);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 5000,
            $"Expected the wait to be bounded by the 300ms " +
            $"timeout, but it took {stopwatch.ElapsedMilliseconds}ms."
        );
    }

    [Fact]
    public void Acquire_Dispose_RemovesEntryFromInProcessLockTable()
    {
        var fileName = "keylock-refcount-" + Guid.NewGuid();

        Assert.False(KeyLock.IsTracked(fileName));

        using (KeyLock.Acquire(fileName, TimeSpan.FromSeconds(5)))
        {
            Assert.True(KeyLock.IsTracked(fileName));
        }

        Assert.False(KeyLock.IsTracked(fileName));
    }

    [Fact(Timeout = 10000)]
    public async Task Acquire_ConcurrentHoldsOnSameKey_EntryRemainsUntilLastReleases()
    {
        var fileName = "keylock-refcount-concurrent-" + Guid.NewGuid();

        var first = KeyLock.Acquire(fileName, TimeSpan.FromSeconds(10));

        var secondTask = Task.Run(() => KeyLock.AcquireAsync(
                fileName,
                TimeSpan.FromSeconds(10),
                CancellationToken.None
            )
        );

        // Give the second attempt a moment to actually start waiting on the semaphore.
        await Task.Delay(200);
        Assert.True(KeyLock.IsTracked(fileName));

        first.Dispose();

        var second = await secondTask;
        Assert.True(KeyLock.IsTracked(fileName)); // second is now holding it

        second.Dispose();

        Assert.False(KeyLock.IsTracked(fileName)); // fully released, entry removed
    }

    [Fact]
    public async Task Acquire_ManySequentialKeys_DoesNotLeaveEntriesBehind()
    {
        for (var i = 0; i < 200; i++)
        {
            var fileName = "keylock-churn-" + i;
            using var _ = await KeyLock.AcquireAsync(fileName, TimeSpan.FromSeconds(5), CancellationToken.None);
        }

        // Table should reflect only what's currently held, not everything ever touched.
        Assert.False(KeyLock.IsTracked("keylock-churn-0"));
        Assert.False(KeyLock.IsTracked("keylock-churn-199"));
    }

    [Fact(Timeout = 10000)]
    public async Task AcquireAsync_CancelledWhileWaitingOnHeldKey_DoesNotLeakEntry()
    {
        // SemaphoreSlim.WaitAsync throws OperationCanceledException on cancellation rather than
        // returning false, which can bypass the "timed out" cleanup path if not handled explicitly.
        var fileName = "keylock-cancel-" + Guid.NewGuid();

        var holder = KeyLock.Acquire(fileName, TimeSpan.FromSeconds(30));

        using var cts = new CancellationTokenSource();
        var waitTask = KeyLock.AcquireAsync(fileName, TimeSpan.FromSeconds(30), cts.Token);

        await Task.Delay(100, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);

        holder.Dispose();

        Assert.False(KeyLock.IsTracked(fileName),
            "A cancelled acquire attempt must not leave its rented entry stuck in the table.");
    }

    [Fact(Timeout = 10000)]
    public void Dispose_CalledConcurrentlyFromMultipleThreads_OnlyReleasesOnce()
    {
        // The disposal guard used to be a plain bool checked then set non-atomically, so two
        // threads racing into Dispose() at the same time could both pass the check and both run
        // cleanup, double-releasing the semaphore (SemaphoreFullException) since it only ever
        // hands out one permit. Not reachable through this library's own usage (a LockHandle is
        // never shared across threads or disposed more than once by our own code), but hardened
        // with Interlocked so it's safe even if that ever changes.
        var fileName = "dispose-race-" + Guid.NewGuid();
        var handle = KeyLock.Acquire(fileName, TimeSpan.FromSeconds(5));

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(handle.Dispose)).ToArray();

        var exception = Record.Exception(() => Task.WaitAll(tasks));

        Assert.Null(exception);
        Assert.False(KeyLock.IsTracked(fileName));
    }
}