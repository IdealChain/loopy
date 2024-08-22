using Loopy.Comm.Interfaces;
using Loopy.Comm.NdcMessages;
using Loopy.Comm.Rpc;
using Loopy.Comm.Sockets;
using Loopy.Core.Data;
using Loopy.Core.Test.LocalCluster;
using NetMQ;
using NUnit.Framework;

namespace Loopy.Comm.Test.Rpc;

public class NodeApiTests
{
    [Test]
    public void TestNodeApi()
    {
        using var runtime = new NetMQRuntime();
        var cluster = new LocalNodeCluster(1);
        var server = new NetMQRpcServer<NdcMessage>(NetMQRpcDefaults.NodeApiPort, 1);
        var handler = new RpcNodeApiHandler(cluster[1].GetNodeApi());
        using var client = new RpcNodeApi(new NetMQRpcClient<NdcMessage>(1, NetMQRpcDefaults.NodeApiPort));

        async Task UpdateFetchTask()
        {
            Key k = "key";
            Value v = "value";

            var obj = new NdcObject();
            obj.DotValues[(1, 1)] = (v, [1, 2, 3, 4]);

            try
            {
                var updateResult = await client.Update(k, obj);
                Assert.That(updateResult.DotValues.Values.Select(v => v.value), Does.Contain(v));

                var fetchResult = await client.Fetch(k, default);
                Assert.That(fetchResult.DotValues.Values.Select(v => v.value), Does.Contain(v));
            }
            catch (TaskCanceledException e)
            {
                Assert.Fail(e.Message);
            }
        }

        runtime.Run(Task.WhenAny(
            server.ServeAsync(handler, CancellationToken.None),
            UpdateFetchTask()));
    }
}
