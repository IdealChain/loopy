using Loopy.Comm.Interfaces;
using Loopy.Comm.NdcMessages;
using Loopy.Core.Data;
using Loopy.Core.Interfaces;
using NLog;

namespace Loopy.Comm.Rpc;

public class RpcClientApiHandler(IClientApi node) : IRpcServerHandler<NdcMessage, NdcMessage>
{
    private static readonly Logger Logger = LogManager.GetLogger(nameof(RpcClientApiHandler));

    public async Task<NdcMessage?> Process(NdcMessage request, CancellationToken ct)
    {
        return await Handle((dynamic)request, ct);
    }

    private Task<NdcMessage?> Handle(NdcMessage req, CancellationToken ct)
    {
        Logger.Warn("unhandled request msg: {Type}", req.GetType());
        return Task.FromResult<NdcMessage?>(null);
    }

    private async Task<NdcMessage?> Handle(ClientGetRequest getRequest, CancellationToken ct)
    {
        node.ReadQuorum = getRequest.Quorum.GetValueOrDefault(1);
        node.ConsistencyMode = getRequest.Mode.GetValueOrDefault(default);

        var (values, cc) = await node.Get(getRequest.Key, ct);
        return new ClientGetResponse
        {
            Values = values.Where(v => !v.IsEmpty).Select(v => v.Data ?? string.Empty).ToArray(),
            CausalContext = cc,
        };
    }

    private async Task<NdcMessage?> Handle(ClientPutRequest putRequest, CancellationToken ct)
    {
        CausalContext cc = putRequest.CausalContext;

        if (putRequest.Value != null)
            await node.Put(putRequest.Key, putRequest.Value, cc, ct);
        else
            await node.Delete(putRequest.Key, cc, ct);

        return new ClientPutResponse();
    }
}
