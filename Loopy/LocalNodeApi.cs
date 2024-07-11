using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;

namespace Loopy;

public class LocalNodeApi : INodeApi
{
    private readonly Node _node;

    public LocalNodeApi(Node node) => _node = node;

    public NodeId NodeId => _node.Id;

    public async Task<IDisposable> Lock(CancellationToken cancellationToken = default)
    {
        return await _node.NodeLock.EnterAsync(cancellationToken);
    }

    public Task<NdcObject> Fetch(Key k, ConsistencyMode mode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_node.Fetch(k, mode));
    }

    public Task<NdcObject> Update(Key k, NdcObject o, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_node.Update(k, o));
    }

    public void SendUpdate(Key k, NdcObject o) => _node.Update(k, o);

    public Task<SyncResponse> SyncClock(SyncRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_node.SyncClock(request));
    }
}
