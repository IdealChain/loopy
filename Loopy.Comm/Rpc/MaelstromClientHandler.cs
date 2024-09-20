using Loopy.Comm.Interfaces;
using Loopy.Comm.MaelstromMessages;
using Loopy.Core.Data;
using Loopy.Core.Interfaces;
using NLog;
using System.Diagnostics.CodeAnalysis;

namespace Loopy.Comm.Rpc;

public class MaelstromClientHandler(IClientApi client) : IRpcServerHandler<RequestBase, ResponseBase>
{
    /// <summary>
    /// Whether to fake non-atomic compare-and-swap (CAS) support
    /// </summary>
    private static readonly bool EnableNonAtomicCas = true;

    private static readonly ILogger Logger = LogManager.GetLogger(nameof(MaelstromClientHandler));

    private readonly CausalContext _causalContext = CausalContext.Initial;

    public async Task<ResponseBase?> Process(RequestBase request, CancellationToken ct = default)
    {
        // node-to-node requests are handled by the separate NDC handler
        if (request is WrappedNdcRequest)
            return null;

        return await Handle((dynamic)request, ct);
    }

    private Task<ResponseBase> Handle(InitRequest body, CancellationToken ct)
    {
        return Task.FromResult<ResponseBase>(new ErrorResponse(ErrorCode.MalformedRequest, "already initialized"));
    }

    private async Task<ResponseBase> Handle(ReadRequest body, CancellationToken ct)
    {
        var (values, cc) = await client.Get(body.key, cancellationToken: ct);
        if (!GetSingleValue(body.key, values, out var value, out var error))
            return error;

        _causalContext.MergeIn(cc, Math.Max);
        return new ReadOkResponse(value);
    }

    private async Task<ResponseBase> Handle(WriteRequest body, CancellationToken ct)
    {
        // hack to reduce concurrent values: update causal context before write, too
        await UpdateCausalContext(body.key, ct);

        await client.Put(body.key, body.value, _causalContext, ct);
        return new WriteOkResponse();
    }

    private async Task UpdateCausalContext(string key, CancellationToken ct)
    {
        var rq = client.ReadQuorum;
        try
        {
            client.ReadQuorum = 1; // only query the local node
            var (_, cc) = await client.Get(key, cancellationToken: ct);
            _causalContext.MergeIn(cc, Math.Max);
        }
        finally { client.ReadQuorum = rq; }
    }

    private async Task<ResponseBase> Handle(CasRequest body, CancellationToken ct)
    {
        if (!EnableNonAtomicCas)
            return new ErrorResponse(ErrorCode.NotSupported, "cas not supported");

        // yes, this is not very atomic
        var (values, cc) = await client.Get(body.key, cancellationToken: ct);
        if (!GetSingleValue(body.key, values, out var value, out var error))
            return error;

        if (value != body.from)
            return new ErrorResponse(ErrorCode.PreconditionFailed, $"current value is {value}, not {body.from}");

        _causalContext.MergeIn(cc, Math.Max);
        await client.Put(body.key, body.to, _causalContext, ct);
        return new CasOkResponse();
    }

    private bool GetSingleValue(string key, Value[] values, out string value, [NotNullWhen(false)] out ErrorResponse? error)
    {
        value = string.Empty;
        var count = 0;
        for (var i = 0; i < values.Length && count < 2; i++)
        {
            var v = values[i];
            if (v.IsEmpty || string.Equals(v.Data, value))
                continue;

            value = v.Data;
            count++;
        }

        error = count switch
        {
            0 => new ErrorResponse(ErrorCode.KeyDoesNotExist, $"key {key} does not exist"),
            > 1 => new ErrorResponse(ErrorCode.Abort, $"key {key} has concurrent values: {values.AsCsv()}"),
            _ => default
        };

        return count == 1;
    }

    private Task<ResponseBase> Handle(MessageBase msg, CancellationToken _2)
    {
        Logger.Warn("unhandled message: {Type}", msg.GetType().Name);
        return Task.FromResult<ResponseBase>(new ErrorResponse(ErrorCode.NotSupported));
    }
}
