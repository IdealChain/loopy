using NLog;

namespace Loopy.Data;

public class AwaitableLock
{
    private static TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger _logger = LogManager.GetLogger(nameof(AwaitableLock));

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(DefaultTimeout, cancellationToken);
            return new LockToken(_semaphore);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.Warn("lock timeout!");
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
