using Loopy.Comm.Interfaces;
using Loopy.Comm.NdcMessages;
using Loopy.Comm.Rpc;
using Loopy.Comm.Sockets;
using Loopy.Core.Data;
using Loopy.Core.Test;
using Loopy.Core.Test.LocalCluster;
using NetMQ;
using NUnit.Framework;

namespace Loopy.Comm.Test.Rpc;

public class ClientApiTests
{
    [Test]
    public void TestClientApi()
    {
        using var runtime = new NetMQRuntime();
        var cluster = new LocalNodeCluster(1);
        var server = new NetMQRpcServer<NdcMessage>(NetMQRpcDefaults.ClientApiPort, 1);
        var handler = new RpcClientApiHandler(cluster[1].GetClientApi());
        using var client = new RpcClientApi(new NetMQRpcClient<NdcMessage>(1, NetMQRpcDefaults.ClientApiPort));

        async Task PutGetTask()
        {
            Key k = "key";

            try
            {
                await client.Put(k, 1);
                Assert.That(await client.GetValues(k), Values.EqualTo(1));
            }
            catch (TaskCanceledException e)
            {
                Assert.Fail(e.Message);
            }
        }

        runtime.Run(Task.WhenAny(
            server.ServeAsync(handler, CancellationToken.None),
            PutGetTask()));
    }
}
