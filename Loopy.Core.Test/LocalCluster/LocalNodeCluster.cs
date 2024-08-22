using Loopy.Core.Api;
using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;
using System.Collections;

namespace Loopy.Core.Test.LocalCluster;

public class LocalNodeCluster : IEnumerable<NodeId>
{
    private readonly Dictionary<NodeId, NodeContext> _nodes;

    public LocalNodeCluster(int nodeCount)
    {
        var nodeIds = Enumerable.Range(1, nodeCount).Select(n => (NodeId)n);
        var replicationStrategy = new GlobalReplicationStrategy(nodeIds);

        _nodes = nodeIds.Select(id => new NodeContext(id, replicationStrategy, CreateRemoteNodeApi))
            .ToDictionary(ctx => ctx.NodeId, ctx => ctx);
    }

    private INodeApi CreateRemoteNodeApi(NodeId id) => this[id].GetNodeApi();

    public NodeContext this[NodeId id] => _nodes[id];

    public IClientApi GetClientApi(NodeId id, ConsistencyMode consistency = ConsistencyMode.Eventual, NodeId[]? replicationFilter = null)
    {
        var clientApi = this[id].GetClientApi(replicationFilter);
        clientApi.ConsistencyMode = consistency;
        return clientApi;
    }

    public IClientApi GetClientApi(NodeId id, NodeId[] replicationFilter) => this[id].GetClientApi(replicationFilter);

    public IEnumerator<NodeId> GetEnumerator() => _nodes.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
