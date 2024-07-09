using Loopy.Data;
using Loopy.Enums;

namespace Loopy.Interfaces;

/// <summary>
/// Client API at Node
/// </summary>
public interface IClientApi
{
    /// <summary>
    /// Number of replica nodes to contact for queries
    /// </summary>
    int ReadQuorum { get; set; }

    /// <summary>
    /// Consistency model to use for queries
    /// </summary>
    ConsistencyMode ConsistencyMode { get; set; }

    /// <summary>
    /// Causal context returned from last query
    /// </summary>
    CausalContext CausalContext { get; set; }

    /// <summary>
    /// Fetches the given key from quorum replicas and
    /// returns all concurrent values along with the causal context 
    /// </summary>
    Task<(Value[] values, CausalContext cc)> Get(Key k, CancellationToken cancellationToken = default);

    /// <summary>
    ///  Forms a new version of the object identified by the given key,
    ///  sending the object to other nodes that replicate that key
    /// </summary>
    Task Put(Key k, Value v, CausalContext? cc = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forms a new, deleted version of the object,
    /// sending the empty value to other nodes that replicate that key
    /// </summary>
    Task Delete(Key k, CausalContext? cc = default, CancellationToken cancellationToken = default);
}
