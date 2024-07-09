using Loopy.Comm.Network;
using Loopy.Data;
using Loopy.Test.LocalCluster;
using NetMQ;
using NUnit.Framework;

namespace Loopy.Test.Comm;

public class ApiTests
{
    [Test]
    public void TestClientApi()
    {
        var cluster = new LocalNodeCluster(1);
        var api = cluster.GetClientApi(1);

        using var runtime = new NetMQRuntime();
        var server = new ClientApiServer(api, "127.0.0.5");
        using var client = new RemoteClientApi("127.0.0.5");

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

        runtime.Run(Task.WhenAny(server.HandleRequests(CancellationToken.None), PutGetTask()));
    }

    [Test]
    public void TestNodeApi()
    {
        var cluster = new LocalNodeCluster(1);
        var api = cluster.GetNodeApi(1);

        using var runtime = new NetMQRuntime();
        var server = new NodeApiServer(api, "127.0.0.6");
        using var client = new RemoteNodeApi("127.0.0.6");

        async Task UpdateFetchTask()
        {
            Key k = "key";
            Value v = "value";

            var obj = new Loopy.Data.Object();
            obj.DotValues[(1, 1)] = v;

            try
            {
                var updateResult = await client.Update(k, obj);
                Assert.That(updateResult.DotValues.Values, Does.Contain(v));

                var fetchResult = await client.Fetch(k, default);
                Assert.That(fetchResult.DotValues.Values, Does.Contain(v));
            }
            catch (TaskCanceledException e)
            {
                Assert.Fail(e.Message);
            }
        }

        runtime.Run(Task.WhenAny(server.HandleRequests(CancellationToken.None), UpdateFetchTask()));
    }
}
