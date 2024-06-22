using Loopy.Comm.Messages;
using Loopy.Interfaces;
using NetMQ;
using NetMQ.Sockets;

namespace Loopy.Comm.Network;

public class NodeApiServer : IDisposable
{
    public const ushort Port = 1337;

    private readonly IRemoteNodeApi _node;
    private readonly NetMQSocket _netMqSocket;

    public NodeApiServer(IRemoteNodeApi node, string host = "*")
    {
        _node = node;
        _netMqSocket = new ResponseSocket($"@tcp://{host}:{Port}");
    }

    public void Dispose()
    {
        _netMqSocket.Dispose();
    }

    public async Task HandleRequests(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var req = await _netMqSocket.ReceiveMessage(cancellationToken);
            await Handle((dynamic)req);
        }
    }

    private Task Handle(IMessage _)
    {
        _netMqSocket.SendFrameEmpty(); // fallback: ack with empty frame
        return Task.CompletedTask;
    }

    private async Task Handle(NodeFetchRequest fetchRequest)
    {
        var fetchResult = await _node.Fetch(fetchRequest.Key, fetchRequest.Mode);
        _netMqSocket.SendMessage(new NodeFetchResponse { Obj = fetchResult });
    }

    private async Task Handle(NodeUpdateRequest updateRequest)
    {
        var updateResult = await _node.Update(updateRequest.Key, updateRequest.Obj);
        _netMqSocket.SendMessage(new NodeUpdateResponse { Obj = updateResult });
    }

    private async Task Handle(NodeSyncClockRequest syncRequest)
    {
        var syncResult = await _node.SyncClock(syncRequest.NodeId, syncRequest.NodeClock);
        _netMqSocket.SendMessage(new NodeSyncClockResponse
        {
            NodeClock = syncResult.NodeClock,
            MissingObjects = syncResult.missingObjects.ToDictionary(t => t.Item1.Name, t => (ObjectMsg)t.Item2),
        });
    }
}
