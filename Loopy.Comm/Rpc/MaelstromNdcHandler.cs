using Loopy.Comm.Interfaces;
using Loopy.Comm.MaelstromMessages;
using Loopy.Comm.NdcMessages;
using Loopy.Core.Interfaces;

namespace Loopy.Comm.Rpc;

public class MaelstromNdcHandler(IRpcServerHandler<NdcMessage, NdcMessage> ndcHandler)
    : IRpcServerHandler<RequestBase, ResponseBase>
{
    public MaelstromNdcHandler(INodeApi nodeApi) : this(new RpcNodeApiHandler(nodeApi))
    { }

    public async Task<ResponseBase?> Process(RequestBase request, CancellationToken ct = default)
    {
        // maelstrom client requests are handled by the separate client handler
        if (request is not WrappedNdcRequest wrappedRequest)
            return null;

        var ndcResponse = await ndcHandler.Process(wrappedRequest.msg, ct);
        if (ndcResponse == null)
            return null;

        return new WrappedNdcResponse(ndcResponse);
    }
}
