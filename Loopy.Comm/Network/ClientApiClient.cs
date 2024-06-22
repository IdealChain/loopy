using Loopy.Comm.Messages;
using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NetMQ;
using NetMQ.Sockets;

namespace Loopy.Comm.Network;

public class ClientApiClient : IDisposable, IClientApi
{
    private readonly NetMQSocket _netMqSocket;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public ClientApiClient(string host = "localhost")
    {
        Host = host;
        _netMqSocket = new RequestSocket($">tcp://{host}:{ClientApiServer.Port}");
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _netMqSocket.Dispose();
    }
    
    public string Host { get; }

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, int quorum = 1, ConsistencyMode mode = default)
    {
        var req = new ClientGetRequest { Key = k.Name, Quorum = quorum, Mode = mode };
        var resp = await _netMqSocket.RemoteCall<ClientGetRequest, ClientGetResponse>(req, _cancellationTokenSource.Token);

        return (
            resp.Values?.Select(v => new Value(v))?.ToArray() ?? Array.Empty<Value>(),
            resp.CausalContext);
    }

    public async Task Put(Key k, Value v, CausalContext? cc = default, ReplicationMode mode = default)
    {
        var req = new ClientPutRequest
        {
            Key = k.Name,
            Value = v.Data,
            Mode = mode,
        };

        if (cc != null)
            req.CausalContext = cc;

        var resp = await _netMqSocket.RemoteCall<ClientPutRequest, ClientPutResponse>(req, _cancellationTokenSource.Token);
        if (!resp.Success)
            throw new InvalidOperationException();
    }

    public Task Delete(Key k, CausalContext? cc = default, ReplicationMode mode = default)
    {
        return Put(k, Value.None, cc, mode);
    }
}
