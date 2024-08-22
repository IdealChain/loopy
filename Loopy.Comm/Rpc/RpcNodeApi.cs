using Loopy.Comm.Interfaces;
using Loopy.Comm.NdcMessages;
using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;

namespace Loopy.Comm.Rpc;

public class RpcNodeApi(IRpcClientSocket<NdcMessage> socket) : INodeApi, IDisposable
{
    public void Dispose() => socket.Dispose();

    public async Task<NdcObject> Fetch(Key k, ConsistencyMode mode, CancellationToken cancellationToken = default)
    {
        var req = new NodeFetchRequest { Key = k.Name, Mode = mode };
        var resp = (NodeFetchResponse)await socket.CallAsync(req, cancellationToken);
        return resp.Obj ?? new NdcObject();
    }

    public async Task<NdcObject> Update(Key k, NdcObject o, CancellationToken cancellationToken = default)
    {
        var req = new NodeUpdateRequest { Key = k.Name, Obj = o, SendResponse = true };
        var resp = (NodeUpdateResponse)await socket.CallAsync(req, cancellationToken);
        return resp.Obj ?? new NdcObject();
    }

    public async Task SendUpdate(Key k, NdcObject o, CancellationToken cancellationToken = default)
    {
        var req = new NodeUpdateRequest { Key = k.Name, Obj = o, SendResponse = false };
        await socket.SendAsync(req, cancellationToken);
    }

    public async Task<SyncResponse> SyncClock(SyncRequest request, CancellationToken cancellationToken = default)
    {
        return (NodeSyncResponse)await socket.CallAsync((NodeSyncRequest)request, cancellationToken);
    }
}
