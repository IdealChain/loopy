using Loopy.Comm.Network;
using Loopy.Data;
using Loopy.Interfaces;

namespace Loopy.NodeShell;

internal class SingleNodeContext : INodeContext, IDisposable
{
    private readonly NodeId _nodeId;
    private readonly NodeId[] _peers;
    private readonly Dictionary<NodeId, IRemoteNodeApi> _clients = new();

    public SingleNodeContext(NodeId nodeId, IEnumerable<NodeId> peers)
    {
        _nodeId = nodeId;
        _peers = peers.ToArray();

        Node = new Node(nodeId, this);
         _clients[nodeId] = new RemoteNodeApiWrapper(Node);
    }

    public Node Node { get; }

    public IEnumerable<NodeId> GetReplicaNodes(Key key) => _peers.Prepend(_nodeId);

    public IEnumerable<NodeId> GetPeerNodes(NodeId n) => _peers.Prepend(_nodeId);

    public IRemoteNodeApi GetNodeApi(NodeId id)
    {
        if (!_clients.TryGetValue(id, out var client))
            client = _clients[id] = new NodeApiClient(GetHost(id));

        return client;
    }

    public string GetHost(NodeId id) => $"127.0.0.{id.Id}";

    public void Dispose()
    {
        foreach (var client in _clients.Values.OfType<IDisposable>())
            client.Dispose();

        _clients.Clear();
    }
}
