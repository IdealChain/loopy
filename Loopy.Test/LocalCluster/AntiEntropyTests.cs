using Loopy.Data;
using Loopy.Interfaces;

namespace Loopy.Test.LocalCluster;

public class AntiEntropyTests
{
    [Test]
    public async Task Test2Nodes()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        await n1.Put("a", 1, CausalContext.Initial, ReplicationMode.None);
        await n1.Put("a", 2, CausalContext.Initial, ReplicationMode.None);
        await n2.Put("b", 7, CausalContext.Initial, ReplicationMode.None);

        var (r1, _) = await n2.Get("a");
        Assert.That(r1, Is.Empty);

        await c.RunBackgroundTasksOnce();

        var (r2, _) = await n2.Get("a");
        Assert.That(r2, Is.EquivalentTo(new Value[] { 2 }));

        var (r3, _) = await n1.Get("b");
        Assert.That(r3, Is.EquivalentTo(new Value[] { 7 }));
    }

    [Test]
    public async Task Test4Nodes()
    {
        var c = new LocalNodeCluster(4);
        foreach (var n in c)
        {
            var api = c.GetClientApi(n);
            await api.Put(n.ToString(), n.Id, CausalContext.Initial, ReplicationMode.None);
            await api.Put("key", n.Id, CausalContext.Initial, ReplicationMode.None);
        }

        c.StartBackgroundTasks(.1, .1);
        await Task.Delay(500);
        await c.StopBackgroundTasks();

        foreach (var n in c)
        {
            var api = c.GetClientApi(n);
            
            // key should conflict 1-4
            var (rKey, _) = await api.Get("key");
            Assert.That(rKey, Is.EquivalentTo(new Value[] { 1, 2, 3, 4 }));

            // all node IDs should have propagated
            foreach (var m in c)
            {
                var (rM, _) = await api.Get(m.ToString());
                Assert.That(rM, Is.EquivalentTo(new Value[] { m.Id }));
            }
        }
    }
}