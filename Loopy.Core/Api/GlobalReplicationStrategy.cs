using Loopy.Core.Data;
using Loopy.Core.Interfaces;
using System.Collections.ObjectModel;

namespace Loopy.Core.Api;

/// <summary>
/// Places a copy of every key value on every node in the cluster -
/// best availability, minimal read latency
/// </summary>
public class GlobalReplicationStrategy : IReplicationStrategy
{
    private readonly ReadOnlyCollection<NodeId> _nodes;

    public GlobalReplicationStrategy(IEnumerable<NodeId> nodes)
    {
        _nodes = nodes.OrderBy(n => n.Id).Distinct().ToList().AsReadOnly();
    }

    public IEnumerable<NodeId> GetReplicaNodes(Key key) => _nodes;

    public IEnumerable<NodeId> GetPeerNodes(NodeId n) => _nodes;
}
