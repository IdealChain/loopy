using Loopy.Comm.Messages;
using Loopy.Data;
using NetMQ.Sockets;
using NLog;

namespace Loopy.Comm.Network;

public class ClientApiServer(LocalClientApi node, string host = "*")
{
    public const ushort Port = 1338;

    private readonly Logger _logger = LogManager.GetLogger($"{nameof(ClientApiServer)}({node.NodeId})");
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

    private async Task<IMessage?> Handle(ClientGetRequest getRequest)
    {
        node.ReadQuorum = getRequest.Quorum.GetValueOrDefault(1);
        node.ConsistencyMode = getRequest.Mode.GetValueOrDefault(default);

        var (values, cc) = await node.Get(getRequest.Key);
        return new ClientGetResponse
        {
            Values = values.Select(v => v.Data).ToArray(),
            CausalContext = cc,
        };
    }

    private async Task<IMessage?> Handle(ClientPutRequest putRequest)
    {
        var cc = CausalContext.Initial;
        if (putRequest.CausalContext != null)
            cc.MergeIn((CausalContext)putRequest.CausalContext);

        if (putRequest.Value != Value.None)
            await node.Put(putRequest.Key, putRequest.Value, cc);
        else
            await node.Delete(putRequest.Key, cc);

        return new ClientPutResponse { Success = true };
    }
}
