using Loopy.Core.Data;

namespace Loopy.Core.Interfaces;

/// <summary>
/// Strategy controlling the replica placement of key values on nodes
/// </summary>
public interface IReplicationStrategy
{
    /// <summary>
    /// Gets all nodes that should be replicas for a given key
    /// </summary>
    IEnumerable<NodeId> GetReplicaNodes(Key key);

    /// <summary>
    /// Gets the peer nodes for the given node, i.e., all nodes that share
    /// replicas of some objects (including the node itself)
    /// </summary>
    IEnumerable<NodeId> GetPeerNodes(NodeId n);
}
