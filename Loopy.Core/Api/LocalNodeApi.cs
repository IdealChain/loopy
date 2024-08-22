using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;

namespace Loopy.Core.Api;

internal class LocalNodeApi(Node node) : INodeApi
{
    public Task<NdcObject> Fetch(Key k, ConsistencyMode mode, CancellationToken cancellationToken = default)
    {
        return node.Fetch(k, mode, cancellationToken);
    }

    public Task<NdcObject> Update(Key k, NdcObject o, CancellationToken cancellationToken = default)
    {
        return node.Update(k, o, cancellationToken);
    }

    public async Task SendUpdate(Key k, NdcObject o, CancellationToken cancellationToken = default)
    {
        await node.Update(k, o, cancellationToken);
    }

    public Task<SyncResponse> SyncClock(SyncRequest request, CancellationToken cancellationToken = default)
    {
        return node.SyncClock(request, cancellationToken);
    }
}
