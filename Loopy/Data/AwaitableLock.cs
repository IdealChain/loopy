namespace Loopy.Data
{
    public class AwaitableLock
    {
        private SemaphoreSlim _semaphore = new(1, 1);

        public async Task<IDisposable> Enter(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new LockToken(_semaphore);
        }

        private sealed class LockToken : IDisposable
        {
            private SemaphoreSlim? _semaphore;

            public LockToken(SemaphoreSlim semaphore) => _semaphore = semaphore;

            public void Dispose()
            {
                _semaphore?.Release();
                _semaphore = null;
            }
        }
    }
}
