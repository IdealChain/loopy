using Loopy.Comm.Interfaces;
using Loopy.Comm.MaelstromMessages;
using Loopy.Comm.NdcMessages;
using System.Collections.Concurrent;

namespace Loopy.Comm.Sockets;

public class MaelstromNdcClient : IRpcClientSocket<NdcMessage>
{
    private readonly string _ownAddress;
    private readonly string _destAddress;
    private readonly IAsyncSocket<Envelope> _socket;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<WrappedNdcResponse>> _pending = new();
    private readonly CancellationTokenSource _cts = new();

    public MaelstromNdcClient(string ownAddress, string destAddress, IAsyncSocket<Envelope> socket)
    {
        _ownAddress = ownAddress;
        _destAddress = destAddress;
        _socket = socket;
        _ = ProcessResponses(_cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();

        foreach (var tcs in _pending.Values)
            tcs.TrySetCanceled();
        _pending.Clear();
    }

    private async Task ProcessResponses(CancellationToken ct)
    {
        await foreach (var msg in _socket.ReceiveAllAsync(ct))
        {
            if (string.Equals(msg.dest, _ownAddress, StringComparison.Ordinal) &&
                msg.body is WrappedNdcResponse response &&
                _pending.TryGetValue(response.in_reply_to, out var tcs))
            {
                tcs.TrySetResult(response);
            }
        }
    }

    public async Task<NdcMessage> CallAsync(NdcMessage request, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<WrappedNdcResponse>();
        ct.Register(() => tcs.TrySetCanceled());

        // create envelope and ensure the message has an unique ID
        var envelope = new Envelope
        {
            src = _ownAddress,
            dest = _destAddress,
            body = new WrappedNdcRequest(request)
            {
                msg_id = MessageBase.GetUniqueId()
            },
        };

        if (!_pending.TryAdd(envelope.body.msg_id, tcs))
            throw new InvalidOperationException("Generated message ID not unique");

        try
        {
            await _socket.SendAsync(envelope, ct);
            return (await tcs.Task).msg;
        }
        finally
        {
            if (_pending.TryRemove(envelope.body.msg_id, out var removed))
                removed.TrySetCanceled();
        }
    }

    public async Task SendAsync(NdcMessage message, CancellationToken ct = default)
    {
        var envelope = new Envelope
        {
            src = _ownAddress,
            dest = _destAddress,
            body = new WrappedNdcRequest(message)
            {
                msg_id = MessageBase.GetUniqueId()
            },
        };

        await _socket.SendAsync(envelope, CancellationToken.None);
    }
}
