using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;

namespace Loopy.Core.Api;

internal class LocalClientApi(Node node) : IClientApi
{
    public int ReadQuorum { get; set; } = 1;
    public ConsistencyMode ConsistencyMode { get; set; } = ConsistencyMode.Eventual;
    public NodeId[]? ReplicationFilter { get; set; } = null;

    public Task<(Value[] values, CausalContext cc)> Get(Key k, CancellationToken cancellationToken = default)
    {
        return node.Get(k, ReadQuorum, ConsistencyMode, cancellationToken);
    }

    public Task Put(Key k, Value v, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        return node.Put(k, v, cc, ReplicationFilter, cancellationToken);
    }

    public Task Delete(Key k, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        return node.Delete(k, cc, ReplicationFilter, cancellationToken);
    }
}
