namespace Loopy.Comm.Interfaces;

public interface IAsyncSocket<T>
{
    IAsyncEnumerable<T> ReceiveAllAsync(CancellationToken ct = default);

    Task SendAsync(T message, CancellationToken ct = default);
}

public interface IRpcClientSocket<T> : IDisposable
{
    Task<T> CallAsync(T request, CancellationToken ct = default);

    Task SendAsync(T message, CancellationToken ct = default);
}

public interface IRpcServerHandler<TReq, TRes>
{
    Task<TRes?> Process(TReq request, CancellationToken ct = default);
}
