using Loopy.Comm.Interfaces;
using Loopy.Core.Data;
using NetMQ.Sockets;
using NLog;

namespace Loopy.Comm.Sockets;

public class NetMQRpcServer<T>(ushort port, string host = "*") where T : class
{
    private static readonly Logger Logger = LogManager.GetLogger(nameof(NetMQRpcServer<T>));

    public NetMQRpcServer(ushort port, NodeId nodeId) : this(port, NetMQRpcDefaults.Localhost(nodeId))
    { }

    public TimeSpan RpcTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public async Task ServeAsync(IRpcServerHandler<T, T> handler, CancellationToken ct)
    {
        using (var routerSocket = new RouterSocket($"@tcp://{host}:{port}"))
        {
            Logger.Info("ready ({Host}:{Port})", host, port);
            var protobufSocket = new NetMQProtobufSocket<T>(routerSocket);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var (request, header) = await protobufSocket.ReceiveAsync(ct);
                    var response = await ProcessRequest(handler, request, ct);
                    if (response != null)
                        await protobufSocket.SendAsync((response, header), ct);
                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    Logger.Error(e, "msg processing failed: {Ex}", e);
                }
            }
        }
    }

    private async Task<T?> ProcessRequest(IRpcServerHandler<T, T> handler, T request, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RpcTimeout);

        try
        {
            // we could return immediately on timeout instead of waiting for the processing to cancel -
            // however, this could leave the NetMQ socket blocked, which is harder to debug
            return await handler.Process(request, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Logger.Warn("{Type} timeout after {Secs}s", request.GetType().Name, RpcTimeout.TotalSeconds);
            return null;
        }
    }
}
