using Loopy.Comm.Interfaces;
using Loopy.Core.Data;
using NetMQ;
using NetMQ.Sockets;

namespace Loopy.Comm.Sockets;

public class NetMQRpcClient<T> : IRpcClientSocket<T> where T : class
{
    private RequestSocket _requestSocket;
    private NetMQProtobufSocket<T> _requestProtobufSocket;
    private DealerSocket _dealerSocket;
    private NetMQProtobufSocket<T> _dealerProtobufSocket;

    public NetMQRpcClient(string host, uint port)
    {
        // reliable request-reply pattern: tolerate lost replies
        _requestSocket = new RequestSocket($">tcp://{host}:{port}");
        _requestSocket.Options.Correlate = true;
        _requestSocket.Options.Relaxed = true;
        _requestProtobufSocket = new NetMQProtobufSocket<T>(_requestSocket);

        // dealer socket for fire-and-forget messages
        _dealerSocket = new DealerSocket($">tcp://{host}:{port}");
        _dealerProtobufSocket = new NetMQProtobufSocket<T>(_dealerSocket);
    }

    public NetMQRpcClient(NodeId nodeId, uint port) : this(NetMQRpcDefaults.Localhost(nodeId), port)
    { }

    public void Dispose()
    {
        _requestSocket.Dispose();
        _dealerSocket.Dispose();
    }

    public async Task<T> CallAsync(T request, CancellationToken ct = default)
    {
        await _requestProtobufSocket.SendAsync((request, null), ct);
        var (resp, _) = await _requestProtobufSocket.ReceiveAsync(ct);
        return resp;
    }

    public async Task SendAsync(T message, CancellationToken ct = default)
    {
        // empty header must be passed so that an empty frame delimiter is inserted
        await _dealerProtobufSocket.SendAsync((message, new NetMQMessage()), ct);
    }
}
