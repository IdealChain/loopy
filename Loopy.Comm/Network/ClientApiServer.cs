using Loopy.Comm.Messages;
using Loopy.Data;
using Loopy.Interfaces;
using NetMQ;
using NetMQ.Sockets;

namespace Loopy.Comm.Network;

public class ClientApiServer : IDisposable
{
    public const ushort Port = 5555;

    private readonly IClientApi _node;
    private readonly NetMQSocket _netMqSocket;

    public ClientApiServer(IClientApi node, string host = "*")
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

    private async Task Handle(ClientGetRequest getRequest)
    {
        var getResult = await _node.Get(
            getRequest.Key,
            getRequest.Quorum.GetValueOrDefault(1),
            getRequest.Mode.GetValueOrDefault(default));

        _netMqSocket.SendMessage(new ClientGetResponse
        {
            Values = getResult.values.Select(v => v.Data).ToArray(), CausalContext = getResult.cc,
        });
    }

    private async Task Handle(ClientPutRequest putRequest)
    {
        var cc = CausalContext.Initial;
        if (putRequest.CausalContext != null)
            cc.MergeIn((CausalContext)putRequest.CausalContext);
        var mode = putRequest.Mode.GetValueOrDefault();

        if (putRequest.Value != Value.None)
            await _node.Put(putRequest.Key, putRequest.Value, cc, mode);
        else
            await _node.Delete(putRequest.Key, cc, mode);

        _netMqSocket.SendMessage(new ClientPutResponse { Success = true });
    }
}
