using Loopy.Core.Api;
using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;
using Loopy.Core.Test.Observation;
using System.Collections;

namespace Loopy.Core.Test.LocalCluster;

public class LocalNodeCluster : IEnumerable<NodeId>
{
    private readonly Dictionary<NodeId, NodeContext> _nodes;
    private readonly MaelstromHistory _history = new();

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
        var clientApi = new RecordingClientApi(this[id].GetClientApi(replicationFilter), _history, id.Id - 1);
        clientApi.ConsistencyMode = consistency;
        return clientApi;
    }

    public IClientApi GetClientApi(NodeId id, NodeId[] replicationFilter)
    {
        return new RecordingClientApi(this[id].GetClientApi(replicationFilter), _history, id.Id - 1);
    }

    public void SaveMaelstromHistory(string name)
    {
        if (_history.HasEntries(ConsistencyMode.Eventual))
        {
            using var evStream = File.Open($"{name}_ev.edn", FileMode.Create);
            _history.Save(ConsistencyMode.Eventual, evStream);
        }

        if (_history.HasEntries(ConsistencyMode.Fifo))
        {
            using var fifoStream = File.Open($"{name}_fifo.edn", FileMode.Create);
            _history.Save(ConsistencyMode.Fifo, fifoStream);
        }
    }

    public IEnumerator<NodeId> GetEnumerator() => _nodes.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
