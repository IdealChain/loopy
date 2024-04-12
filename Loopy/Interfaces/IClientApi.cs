using Loopy.Data;

namespace Loopy.Interfaces;

public enum ReplicationMode
{
    Sync,
    Async,
    None,
}

public enum ConsistencyMode
{
    Eventual,
    Fifo,
    Causal,
}

/// <summary>
/// Client API at Node
/// </summary>
public interface IClientApi
{
    /// <summary>
    /// Fetches the given key from quorum replicas and
    /// returns all concurrent values along with the causal context 
    /// </summary>
    Task<(Value[] values, CausalContext cc)> Get(Key k, int quorum = 1, ConsistencyMode mode = default);

    /// <summary>
    ///  Forms a new version of the object identified by the given key,
    ///  sending the object to other nodes that replicate that key
    /// </summary>
    Task Put(Key k, Value v, CausalContext? cc = default, ReplicationMode mode = default);

    /// <summary>
    /// Forms a new, deleted version of the object,
    /// sending the empty value to other nodes that replicate that key
    /// </summary>
    Task Delete(Key k, CausalContext? cc = default, ReplicationMode mode = default);
}