using Loopy.Comm.Extensions;
using Loopy.Comm.Interfaces;
using NLog;

namespace Loopy.Comm.Sockets;

public class MulticastSocket<T> : IAsyncSocket<T>
{
    private static readonly ILogger Logger = LogManager.GetLogger(nameof(MulticastSocket<T>));

    private readonly IAsyncSocket<T> _socket;
    private readonly MulticastAsyncEnumerable<T> _distributor = new();

    public MulticastSocket(IAsyncSocket<T> socket)
    {
        _socket = socket;
        _ = PumpSource(socket, _distributor);
    }

    public IAsyncEnumerable<T> ReceiveAllAsync(CancellationToken ct = default) => _distributor;

    public Task SendAsync(T message, CancellationToken ct = default) => _socket.SendAsync(message, ct);

    private static async Task PumpSource(IAsyncSocket<T> source, MulticastAsyncEnumerable<T> target,
        CancellationToken ct = default)
    {
        try
        {
            await foreach (var m in source.ReceiveAllAsync(ct))
                target.Write(m);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested) { Logger.Warn(ex); }
        finally { target.Complete(); }
    }
}
