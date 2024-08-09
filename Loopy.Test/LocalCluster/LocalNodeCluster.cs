using Loopy.Core;
using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;
using System.Collections;

namespace Loopy.Test.LocalCluster;

public class LocalNodeCluster : IEnumerable<NodeId>, INodeContext
{
    private readonly Dictionary<NodeId, Node> _nodes = new();
    private readonly Dictionary<NodeId, LocalBackgroundTasks> _backgroundTasks = new();

    public LocalNodeCluster(int nodeCount)
    {
        foreach (var nodeId in Enumerable.Range(1, nodeCount).Select(i => new NodeId(i)))
        {
            _nodes[nodeId] = new Node(nodeId, this);
            _backgroundTasks[nodeId] = new LocalBackgroundTasks(_nodes[nodeId]);
        }
    }

    public IEnumerable<NodeId> GetReplicaNodes(Key key) => _nodes.Keys;

    public IEnumerable<NodeId> GetPeerNodes(NodeId n) => _nodes.Keys;

    public LocalNodeApi GetNodeApi(NodeId id) => new LocalNodeApi(_nodes[id]);

    INodeApi INodeContext.GetNodeApi(NodeId id) => GetNodeApi(id);

    public LocalClientApi GetClientApi(NodeId id) => new LocalClientApi(_nodes[id]);

    public LocalClientApi GetClientApi(NodeId id, ConsistencyMode consistency = ConsistencyMode.Eventual)
    {
        return new LocalClientApi(_nodes[id])
        {
            ConsistencyMode = consistency
        };
    }

    public LocalClientApi GetClientApi(NodeId id, ConsistencyMode consistency, NodeId[] replicationFilter)
    {
        return new LocalClientApi(_nodes[id])
        {
            ConsistencyMode = consistency,
            ReplicationFilter = replicationFilter
        };
    }

    public LocalClientApi GetClientApi(NodeId id, NodeId[] replicationFilter)
    {
        return new LocalClientApi(_nodes[id])
        {
            ReplicationFilter = replicationFilter
        };
    }

    public LocalBackgroundTasks GetBackgroundTasks(NodeId id) => _backgroundTasks[id];

    public IEnumerator<NodeId> GetEnumerator() => _nodes.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
