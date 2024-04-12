using Loopy.Data;
using Object = Loopy.Data.Object;

namespace Loopy.Interfaces;

/// <summary>
/// Node API for other nodes to fetch or push objects
/// </summary>
public interface IRemoteNodeApi
{
    /// <summary>
    /// Returns the object for the given key 
    /// </summary>
    Task<Object> Fetch(Key k, ConsistencyMode mode);
    
    /// <summary>
    /// Merges in the object for the given key and returns the result 
    /// </summary>
    Task<Object> Update(Key k, Object o);

    /// <summary>
    /// Syncs the passed node clock with the own one and returns missing objects 
    /// </summary>
    Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncClock(
        NodeId p, Map<NodeId, UpdateIdSet> nodeClockP);
}