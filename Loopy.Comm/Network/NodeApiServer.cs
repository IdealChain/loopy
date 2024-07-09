using Loopy.Comm.Messages;
using Loopy.Data;
using NetMQ.Sockets;
using NLog;

namespace Loopy.Comm.Network;

public class NodeApiServer(LocalNodeApi node, string host = "*")
{
    public const ushort Port = 1337;

    private readonly Logger _logger = LogManager.GetLogger($"{nameof(NodeApiServer)}({node.NodeId})");
    private RouterSocket? _routerSocket;

    public async Task HandleRequests(CancellationToken cancellationToken)
    {
        try
        {
            using (_routerSocket = new RouterSocket($"@tcp://{host}:{Port}"))
            {
                _logger.Info("ready ({Host}:{Port})", host, Port);
                while (!cancellationToken.IsCancellationRequested)
                {
                    var (request, header) = await _routerSocket.ReceiveMessage(cancellationToken);
                    using (await node.Lock(cancellationToken))
                    {
                        if (await Handle((dynamic)request) is IMessage response)
                            _routerSocket.SendMessage(response, header);
                    }
                }
            }
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.Fatal(e);
            throw;
        }
    }

    private Task<IMessage?> Handle(IMessage req)
    {
        _logger.Warn("unhandled request msg: {Type}", req.GetType());
        return Task.FromResult<IMessage?>(null);
    }

    private async Task<IMessage?> Handle(NodeFetchRequest fetchRequest)
    {
        var fetchResult = await node.Fetch(fetchRequest.Key, fetchRequest.Mode);
        return new NodeFetchResponse { Obj = fetchResult };
    }

    private async Task<IMessage?> Handle(NodeUpdateRequest updateRequest)
    {
        var updateResult = await node.Update(updateRequest.Key, updateRequest.Obj ?? new Data.Object());
        return new NodeUpdateResponse { Obj = updateResult };
    }

    private async Task<IMessage?> Handle(NodeSyncClockRequest syncRequest)
    {
        var (nodeClock, missingObjects) =
            await node.SyncClock(syncRequest.NodeId, syncRequest.NodeClock ?? new Map<NodeId, UpdateIdSet>());
        return new NodeSyncClockResponse
        {
            NodeClock = nodeClock,
            MissingObjects = missingObjects.ToDictionary(t => t.Item1.Name, t => (ObjectMsg)t.Item2),
        };
    }
}
