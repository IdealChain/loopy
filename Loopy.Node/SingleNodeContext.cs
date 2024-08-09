using Loopy.Comm.Network;
using Loopy.Core;
using Loopy.Core.Data;
using Loopy.Core.Interfaces;

namespace Loopy.Node;

public class SingleNodeContext : INodeContext, IDisposable
{
    private readonly NodeId _nodeId;
    private readonly NodeId[] _peers;
    private readonly Dictionary<NodeId, INodeApi> _nodeApis = new();

    public SingleNodeContext(NodeId nodeId, IEnumerable<NodeId> peers)
    {
        _nodeId = nodeId;
        _peers = peers.Where(n => n != _nodeId).ToArray();

        Node = new Core.Node(nodeId, this);
        _nodeApis[nodeId] = new LocalNodeApi(Node);
    }

    public Core.Node Node { get; }

    public IEnumerable<NodeId> GetReplicaNodes(Key key) => _peers.Prepend(_nodeId);

    public IEnumerable<NodeId> GetPeerNodes(NodeId n) => _peers.Prepend(_nodeId);

    public INodeApi GetNodeApi(NodeId id)
    {
        if (!_nodeApis.TryGetValue(id, out var nodeApi))
            nodeApi = _nodeApis[id] = new RemoteNodeApi(GetHost(id));

        return nodeApi;
    }
    
    public string GetHost(NodeId id) => $"127.0.0.{id.Id}";

    public void Dispose()
    {
        foreach (var client in _nodeApis.Values.OfType<IDisposable>())
            client.Dispose();

        _nodeApis.Clear();
    }
}
