using Loopy.Comm.Messages;
using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NetMQ.Sockets;
using System.Diagnostics.CodeAnalysis;
using Object = Loopy.Data.Object;

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

    public async Task<Object> Fetch(Key k, ConsistencyMode mode, CancellationToken cancellationToken = default)
    {
        var req = new NodeFetchRequest { Key = k.Name, Mode = mode };
        var resp = await _requestSocket.RemoteCall<NodeFetchRequest, NodeFetchResponse>(req, cancellationToken);
        return resp.Obj ?? new Object();
    }

    public async Task<Object> Update(Key k, Object o, CancellationToken cancellationToken = default)
    {
        var req = new NodeUpdateRequest { Key = k.Name, Obj = o };
        var resp = await _requestSocket.RemoteCall<NodeUpdateRequest, NodeUpdateResponse>(req, cancellationToken);
        return resp.Obj ?? new Object();
    }

    public void SendUpdate(Key k, Object o)
    {
        var req = new NodeUpdateRequest { Key = k.Name, Obj = o };
        _dealerSocket.SendMessage(req, new NetMQ.NetMQMessage());
    }

    public async Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncClock(
        NodeId peer, Map<NodeId, UpdateIdSet> nodeClockPeer, CancellationToken cancellationToken = default)
    {
        var req = new NodeSyncClockRequest
        {
            NodeId = peer.Id,
            NodeClock = nodeClockPeer,
        };
        var resp = await _requestSocket.RemoteCall<NodeSyncClockRequest, NodeSyncClockResponse>(req, cancellationToken);
        var nc = resp.NodeClock ?? new Map<NodeId, UpdateIdSet>();
        var missingObjs = new List<(Key, Object)>();
        if (resp.MissingObjects != null)
            missingObjs.AddRange(resp.MissingObjects.Select(kv => ((Key)kv.Key, (Object)kv.Value)));

        return (nc, missingObjs);
    }

    public async Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncFifoClock(
        NodeId peer, Priority prio, Map<NodeId, UpdateIdSet> nodeClockPeer, CancellationToken cancellationToken = default)
    {
        var nc = new Map<NodeId, UpdateIdSet>();
        var missingObjs = new List<(Key, Object)>();

        // TODO

        return (nc, missingObjs);
    }
}
