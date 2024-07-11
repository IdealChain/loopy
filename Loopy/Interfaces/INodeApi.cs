using Loopy.Data;
using Loopy.Enums;

namespace Loopy.Interfaces;

/// <summary>
/// Node API for other nodes to fetch or push objects
/// </summary>
public interface INodeApi
{
    /// <summary>
    /// Returns the object for the given key 
    /// </summary>
    Task<NdcObject> Fetch(Key k, ConsistencyMode mode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Merges in the object for the given key and returns the result 
    /// </summary>
    Task<NdcObject> Update(Key k, NdcObject o, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges in the object for the given key, without waiting for the result
    /// </summary>
    void SendUpdate(Key k, NdcObject o);

    /// <summary>
    /// Syncs the passed node clock with the own one and returns any missing objects
    /// </summary>
    Task<SyncResponse> SyncClock(SyncRequest request, CancellationToken cancellationToken = default);
}
