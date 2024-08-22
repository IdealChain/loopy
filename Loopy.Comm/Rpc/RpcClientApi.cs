using Loopy.Comm.Interfaces;
using Loopy.Comm.NdcMessages;
using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;

namespace Loopy.Comm.Rpc;

public class RpcClientApi(IRpcClientSocket<NdcMessage> socket) : IClientApi, IDisposable
{
    public void Dispose() => socket.Dispose();

    public int ReadQuorum { get; set; } = 1;
    public ConsistencyMode ConsistencyMode { get; set; } = ConsistencyMode.Eventual;

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, CancellationToken cancellationToken = default)
    {
        var req = new ClientGetRequest
        {
            Key = k.Name,
            Quorum = ReadQuorum,
            Mode = ConsistencyMode
        };

        var resp = (ClientGetResponse)await socket.CallAsync(req, cancellationToken);

        Value[]? values = null;
        CausalContext? cc = null;

        if (resp.Values != null && resp.Values.Length > 0)
            values = resp.Values.Select(v => new Value(v)).ToArray();

        if (resp.CausalContext != null)
            cc = resp.CausalContext;

        return (values ?? [], cc ?? CausalContext.Initial);
    }

    public async Task Put(Key k, Value v, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        var req = new ClientPutRequest
        {
            Key = k.Name,
            Value = v.Data,
            CausalContext = cc ?? CausalContext.Initial,
        };

        await socket.CallAsync(req, cancellationToken);
    }

    public async Task Delete(Key k, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        var req = new ClientPutRequest
        {
            Key = k.Name,
            Value = null,
            CausalContext = cc ?? CausalContext.Initial,
        };

        await socket.CallAsync(req, cancellationToken);
    }
}
