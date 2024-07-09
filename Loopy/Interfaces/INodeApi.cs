using Loopy.Data;
using Loopy.Enums;
using Object = Loopy.Data.Object;

namespace Loopy.Interfaces;

/// <summary>
/// Node API for other nodes to fetch or push objects
/// </summary>
public interface INodeApi
{
    /// <summary>
    /// Returns the object for the given key 
    /// </summary>
    Task<Object> Fetch(Key k, ConsistencyMode mode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Merges in the object for the given key and returns the result 
    /// </summary>
    Task<Object> Update(Key k, Object o, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges in the object for the given key, without waiting for the result
    /// </summary>
    void SendUpdate(Key k, Object o);

    /// <summary>
    /// Syncs the passed node clock with the own one and returns missing objects
    /// </summary>
    Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncClock(
        NodeId peer, Map<NodeId, UpdateIdSet> nodeClockPeer, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Syncs the passed node clock with the own one and returns missing objects (FIFO edition)
    /// </summary>
    Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncFifoClock(
        NodeId peer, Priority prio, Map<NodeId, UpdateIdSet> nodeClockPeer, CancellationToken cancellationToken = default);
}
