using Loopy.Comm.Messages;
using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NetMQ.Sockets;
using System.Diagnostics.CodeAnalysis;

namespace Loopy.Comm.Network;

public class RemoteClientApi : IDisposable, IClientApi
{
    private string _host;
    private RequestSocket _requestSocket;

    public RemoteClientApi(string host = "localhost")
    {
        _host = host;
        RecreateSocket();
    }

    public void Dispose() => _requestSocket.Dispose();

    [MemberNotNull(nameof(_requestSocket))]
    private void RecreateSocket()
    {
        _requestSocket?.Dispose();
        _requestSocket = new RequestSocket($">tcp://{_host}:{ClientApiServer.Port}");

        // reliable request-reply pattern: tolerate lost replies
        _requestSocket.Options.Correlate = true;
        _requestSocket.Options.Relaxed = true;
    }

    public string Host
    {
        get => _host;
        set
        {
            _host = value;
            RecreateSocket();
            CausalContext = CausalContext.Initial;
        }
    }

    public int ReadQuorum { get; set; } = 1;
    public ConsistencyMode ConsistencyMode { get; set; } = ConsistencyMode.Eventual;

    /// <summary>
    /// Causal context resulting from all queries issued so far
    /// </summary>
    public CausalContext CausalContext { get; set; } = CausalContext.Initial;

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, CancellationToken cancellationToken = default)
    {
        var req = new ClientGetRequest
        {
            Key = k.Name,
            Quorum = ReadQuorum,
            Mode = ConsistencyMode
        };

        var resp = await _requestSocket.RemoteCall<ClientGetRequest, ClientGetResponse>(req, cancellationToken);

        Value[]? values = null;
        CausalContext? cc = null;

        if (resp.Values != null && resp.Values.Length > 0)
            values = resp.Values.Select(v => new Value(v)).ToArray();

        if (resp.CausalContext != null)
        {
            cc = resp.CausalContext;
            CausalContext.MergeIn(cc, Math.Max);
        }

        return (values ?? [], cc ?? CausalContext.Initial);
    }

    public async Task Put(Key k, Value v, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        var req = new ClientPutRequest
        {
            Key = k.Name,
            Value = v.Data,
            CausalContext = cc ?? CausalContext,
        };

        var resp = await _requestSocket.RemoteCall<ClientPutRequest, ClientPutResponse>(req, cancellationToken);
        if (!resp.Success)
            throw new InvalidOperationException("Put operation failed");
    }

    public async Task Delete(Key k, CausalContext? cc = default, CancellationToken cancellationToken = default)
    {
        var req = new ClientPutRequest
        {
            Key = k.Name,
            Value = null,
            CausalContext = cc ?? CausalContext,
        };

        var resp = await _requestSocket.RemoteCall<ClientPutRequest, ClientPutResponse>(req, cancellationToken);
        if (!resp.Success)
            throw new InvalidOperationException("Put operation failed");
    }
}
