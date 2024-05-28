using Loopy.Comm.Network;
using Loopy.Data;
using Loopy.Enums;
using Loopy.Test.LocalCluster;
using NetMQ;

namespace Loopy.Test.Network;

public class NetMqRpcTests
{
    [Test]
    public Task TestClientApiAsync()
    {
        var cluster = new LocalNodeCluster(1);
        var api = cluster.GetClientApi(1);

        using var runtime = new NetMQRuntime();
        using var server = new ClientApiServer(api, "127.0.0.5");
        using var client = new ClientApiClient("127.0.0.5");
        using var cts = new CancellationTokenSource();

        async Task PutGetTask()
        {
            try
            {
                await client.Put("key", 1);
                var (values, _) = await client.Get("key");
                Assert.That(values, Is.EquivalentTo(new Value[] { 1 }));
            }
            catch (TaskCanceledException e)
            {
                Assert.Fail(e.Message);
            }
            finally { cts.Cancel(); }
        }

        runtime.Run(cts.Token, server.HandleRequests(cts.Token), PutGetTask());

        return Task.CompletedTask;
    }

    [Test]
    public Task TestNodeApiAsync()
    {
        var cluster = new LocalNodeCluster(1);
        var api = cluster.GetNodeApi(1);

        using var runtime = new NetMQRuntime();
        using var server = new NodeApiServer(api, "127.0.0.6");
        using var client = new NodeApiClient("127.0.0.6");
        using var cts = new CancellationTokenSource();

        async Task UpdateFetchTask()
        {
            try
            {
                var updateResult = await client.Update("key",
                    new Loopy.Data.Object
                    {
                        DotValues = new() { { new Dot(1, 1), "value" } },
                        CausalContext = new() { { 1, 1 } },
                        FifoBarriers = new() { { Priority.Bulk, 0 } },
                    });
                Assert.That(updateResult.DotValues.Values, Does.Contain((Value)"value"));

                var fetchResult = await client.Fetch("key", ConsistencyMode.Eventual);
                Assert.That(fetchResult.DotValues.Values, Does.Contain((Value)"value"));
            }
            catch (TaskCanceledException e)
            {
                Assert.Fail(e.Message);
            }
            finally { cts.Cancel(); }
        }

        runtime.Run(cts.Token, server.HandleRequests(cts.Token), UpdateFetchTask());

        return Task.CompletedTask;
    }
}
