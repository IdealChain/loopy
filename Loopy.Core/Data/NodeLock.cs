using NLog;

namespace Loopy.Core.Data;

public class NodeLock
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private static readonly ILogger Logger = LogManager.GetLogger(nameof(NodeLock));

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Enters the lock in READ mode
    /// </summary>
    public Task<IDisposable> EnterReadAsync(CancellationToken ct)
    {
        // in principle, multiple read tasks may be active concurrently -
        // for now, though, we simply share the write lock
        return EnterWriteAsync(ct);
    }

    /// <summary>
    /// Enters the lock in WRITE mode
    /// </summary>
    public async Task<IDisposable> EnterWriteAsync(CancellationToken ct)
    {
        try
        {
            if (!await _semaphore.WaitAsync(DefaultTimeout, ct))
                throw new OperationCanceledException("lock timeout");

            return new LockToken(_semaphore);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Logger.Warn("lock timeout!");
            throw;
        }
    }

    private struct LockToken(SemaphoreSlim? semaphore) : IDisposable
    {
        public void Dispose()
        {
            semaphore?.Release();
            semaphore = null;
        }
    }
}
