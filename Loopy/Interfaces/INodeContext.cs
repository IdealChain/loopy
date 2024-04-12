using Loopy.Data;

namespace Loopy.Interfaces;

/// <summary>
/// Auxillary information for a node to communicate with its peers
/// </summary>
public interface INodeContext
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

    /// <summary>
    /// Gets the node-to-node RPC API 
    /// </summary>
    IRemoteNodeApi GetNodeApi(NodeId id);
}