using Loopy.Comm.Messages;
using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;
using NetMQ.Sockets;
using System.Diagnostics.CodeAnalysis;

namespace Loopy.Comm.Network;

public class RemoteNodeApi : IDisposable, INodeApi
{
    private string _host;
    private RequestSocket _requestSocket;
    private DealerSocket _dealerSocket;

    public RemoteNodeApi(string host)
    {
        _host = host;
        RecreateSockets();
    }

    public void Dispose()
    {
        _requestSocket.Dispose();
        _dealerSocket.Dispose();
    }

    [MemberNotNull(nameof(_requestSocket), nameof(_dealerSocket))]
    private void RecreateSockets()
    {
        _requestSocket?.Dispose();
        _requestSocket = new RequestSocket($">tcp://{Host}:{NodeApiServer.Port}");

        // reliable request-reply pattern: tolerate lost replies
        _requestSocket.Options.Correlate = true;
        _requestSocket.Options.Relaxed = true;

        _dealerSocket?.Dispose();
        _dealerSocket = new DealerSocket($">tcp://{Host}:{NodeApiServer.Port}");
    }

    public string Host
    {
        get => _host;
        set
        {
            _host = value;
            RecreateSockets();
        }
    }

    public async Task<NdcObject> Fetch(Key k, ConsistencyMode mode, CancellationToken cancellationToken = default)
    {
        var req = new NodeFetchRequest { Key = k.Name, Mode = mode };
        var resp = await _requestSocket.RemoteCall<NodeFetchRequest, NodeFetchResponse>(req, cancellationToken);
        return resp.Obj ?? new NdcObject();
    }

    public async Task<NdcObject> Update(Key k, NdcObject o, CancellationToken cancellationToken = default)
    {
        var req = new NodeUpdateRequest { Key = k.Name, Obj = o };
        var resp = await _requestSocket.RemoteCall<NodeUpdateRequest, NodeUpdateResponse>(req, cancellationToken);
        return resp.Obj ?? new NdcObject();
    }

    public void SendUpdate(Key k, NdcObject o)
    {
        var req = new NodeUpdateRequest { Key = k.Name, Obj = o };
        _dealerSocket.SendMessage(req, new NetMQ.NetMQMessage());
    }

    public async Task<SyncResponse> SyncClock(SyncRequest request, CancellationToken cancellationToken = default)
    {
        return await _requestSocket.RemoteCall<NodeSyncRequest, NodeSyncResponse>(request, cancellationToken);
    }
}
