using Loopy.Comm.Interfaces;
using Loopy.Comm.NdcMessages;
using Loopy.Core.Interfaces;
using NLog;

namespace Loopy.Comm.Rpc;

public class RpcNodeApiHandler(INodeApi node) : IRpcServerHandler<NdcMessage, NdcMessage>
{
    private static readonly Logger Logger = LogManager.GetLogger(nameof(RpcNodeApiHandler));

    public async Task<NdcMessage?> Process(NdcMessage request, CancellationToken cancellationToken)
    {
        return await Handle((dynamic)request);
    }

    private Task<NdcMessage?> Handle(NdcMessage req)
    {
        Logger.Warn("unhandled request msg: {Type}", req.GetType());
        return Task.FromResult<NdcMessage?>(null);
    }

    private async Task<NdcMessage?> Handle(NodeFetchRequest fetchRequest)
    {
        var fetchResult = await node.Fetch(fetchRequest.Key, fetchRequest.Mode);
        return new NodeFetchResponse { Obj = fetchResult };
    }

    private async Task<NdcMessage?> Handle(NodeUpdateRequest updateRequest)
    {
        var updateResult = await node.Update(updateRequest.Key, updateRequest.Obj);
        return updateRequest.SendResponse ? new NodeUpdateResponse { Obj = updateResult } : null;
    }

    private async Task<NdcMessage?> Handle(NodeSyncRequest syncRequest)
    {
        var resp = await node.SyncClock(syncRequest);
        return (NodeSyncResponse)resp;
    }
}
