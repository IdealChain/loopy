using Loopy.Core.Data;
using NLog;

namespace Loopy.Core.Interfaces;

/// <summary>
/// Auxillary information containing a node's configuration
/// </summary>
public interface INodeContext
{
    /// <summary>
    /// Gets the node's own node ID
    /// </summary>
    NodeId NodeId { get; }

    /// <summary>
    /// Gets a logging interface for collecting debug info
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Gets the replication strategy that controls the placement of key values on nodes
    /// </summary>
    IReplicationStrategy ReplicationStrategy { get; }

    INotificationStrategy NotificationStrategy { get; }

    /// <summary>
    /// Gets the node-to-node RPC API 
    /// </summary>
    INodeApi GetNodeApi(NodeId id);
}
