using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;

namespace Loopy;

public class LocalClientApi : IClientApi
{
    private readonly Node _node;

    public LocalClientApi(Node node) => _node = node;

    public NodeId NodeId => _node.Id;
    public int ReadQuorum { get; set; } = 1;
    public ConsistencyMode ConsistencyMode { get; set; } = ConsistencyMode.Eventual;
    public NodeId[]? ReplicationFilter { get; set; } = null;
    public CausalContext CausalContext { get; set; } = CausalContext.Initial;

    public async Task<IDisposable> Lock(CancellationToken cancellationToken = default)
    {
        return await _node.NodeLock.EnterAsync(cancellationToken);
    }

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, CancellationToken cancellationToken = default)
    {
        var (values, CausalContext) = await _node.Get(k, ReadQuorum, ConsistencyMode, cancellationToken);
        return (values, CausalContext);
    }

    public async Task Put(Key k, Value v, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        cc = cc ?? CausalContext;
        await _node.Put(k, v, cc, ReplicationFilter, cancellationToken);
    }

    public async Task Delete(Key k, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        cc = cc ?? CausalContext;
        await _node.Delete(k, cc, ReplicationFilter, cancellationToken);
    }
}
