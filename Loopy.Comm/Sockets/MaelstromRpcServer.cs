using Loopy.Comm.Interfaces;
using Loopy.Comm.MaelstromMessages;
using NLog;

namespace Loopy.Comm.Sockets;

public class MaelstromRpcServer(IAsyncSocket<Envelope> socket, string address)
{
    private static readonly Logger Logger = LogManager.GetLogger(nameof(MaelstromRpcServer));

    public TimeSpan RpcTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public async Task ServeAsync(IRpcServerHandler<RequestBase, ResponseBase> handler, CancellationToken ct = default)
    {
        await foreach (var envelope in socket.ReceiveAllAsync(ct))
        {
            if (!string.Equals(envelope.dest, address, StringComparison.Ordinal) ||
                !(envelope.body is RequestBase request))
                continue;

            try
            {
                var response = await ProcessCommand(handler, request, ct);
                if (response != null)
                    await socket.SendAsync(envelope.CreateReply(response.InReplyTo(request)));
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                Logger.Error(e, "msg {Id} processing failed: {Ex}", envelope.body.msg_id, e);
            }
        }
    }

    private async Task<ResponseBase?> ProcessCommand(
        IRpcServerHandler<RequestBase, ResponseBase> handler, RequestBase request, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RpcTimeout);

        try
        {
            // we could return immediately on timeout instead of waiting for the processing to cancel -
            // however, this could leave the underlying NetMQ socket blocked, which is harder to debug
            return await handler.Process(request, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Logger.Warn("{Type} timeout after {Secs}s", request.GetType().Name, RpcTimeout.TotalSeconds);
            return new ErrorResponse(ErrorCode.Timeout, $"timeout after {RpcTimeout.TotalSeconds:N0}s");
        }
    }
}
