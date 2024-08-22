using Loopy.Comm.Interfaces;
using Loopy.Comm.MaelstromMessages;
using Loopy.Comm.Rpc;
using Loopy.Comm.Sockets;
using Loopy.Core.Api;
using Loopy.Core.Data;
using Loopy.Core.Enums;
using NLog;
using System.Diagnostics;

namespace Loopy.MaelstromNode;

public class MaelstromApi(IAsyncSocket<Envelope> msgSocket)
{
    private static readonly ILogger Logger = LogManager.GetLogger(nameof(MaelstromApi));

    public ConsistencyMode ConsistencyMode { get; set; } = ConsistencyMode.Fifo;
    public int ReadQuorum { get; set; } = 1;

    public async Task<(NodeId nodeId, List<NodeId> nodeIds)> WaitForInit(CancellationToken ct)
    {
        Logger.Info("waiting for init...");
        await foreach (var msg in msgSocket.ReceiveAllAsync(ct))
        {
            if (msg.body is not InitRequest init)
            {
                Logger.Warn("ignoring non-init request: {Type}", msg.body.GetType().Name);
                continue;
            }

            Debug.Assert(string.Equals(msg.dest, init.node_id, StringComparison.Ordinal));
            var nodeId = NodeId.Parse(init.node_id);
            var nodeIds = init.node_ids.Select(NodeId.Parse).ToList();
            await msgSocket.SendAsync(msg.CreateReply(new InitOkResponse().InReplyTo(init)), ct);
            return (nodeId, nodeIds);
        }

        throw new OperationCanceledException();
    }

    public async Task RunNode(NodeId nodeId, List<NodeId> nodeIds, CancellationToken ct)
    {
        var replicationStrategy = new GlobalReplicationStrategy(nodeIds.Prepend(nodeId));
        var context = new NodeContext(nodeId, replicationStrategy, id =>
            new RpcNodeApi(new MaelstromNdcClient($"n{nodeId.Id}", $"n{id.Id}", msgSocket)));

        context.BackgroundTasks.Config = new()
        {
            AntiEntropyInterval = TimeSpan.FromSeconds(5),
            AntiEntropyTimeout = TimeSpan.FromSeconds(3),
            HeartbeatInterval = TimeSpan.FromSeconds(3),
        };

        var maelstromServer = new MaelstromRpcServer(msgSocket, $"n{nodeId.Id}");
        var clientApi = context.GetClientApi();
        clientApi.ConsistencyMode = ConsistencyMode;
        clientApi.ReadQuorum = ReadQuorum;
        var nodeApi = context.GetNodeApi();

        Logger.Info("starting {Node} ({Mode}, {Quorum})", nodeId, clientApi.ConsistencyMode, clientApi.ReadQuorum);
        await await Task.WhenAny(
            maelstromServer.ServeAsync(new MaelstromClientHandler(clientApi), ct),
            maelstromServer.ServeAsync(new MaelstromNdcHandler(nodeApi), ct),
            context.BackgroundTasks.Run(ct));
    }
}
