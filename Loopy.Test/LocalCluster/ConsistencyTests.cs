using Loopy.Data;
using Loopy.Interfaces;

namespace Loopy.Test.LocalCluster;

public class ConsistencyTests
{
    private readonly Key a = "a";
    private readonly Key b = "b";
    private readonly Value V0 = 0;
    private readonly Value V1 = 1;
    private readonly Value V2 = 2;

    [Test]
    public async Task TestFifoGet()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        // N1: initialize a=0, b=0
        await n1.Put(a, V0);
        await n1.Put(b, V0);

        // lose write of a=1, then replicate b=2
        await n1.Put(a, V1, mode: ReplicationMode.None);
        await n1.Put(b, V2, mode: ReplicationMode.Sync);

        var n1A = await n1.Get(a);
        var n1B = await n1.Get(b);
        Assert.That(n1A.values.Concat(n1B.values), Values.EquivalentTo(V1, V2));

        // FIFO consistency: we expect to see a=1 BEFORE b=2, never after
        // N2: before anti-entropy, we expect the initial values a=0, b=0
        var n2A = await n2.Get(a, mode: ConsistencyMode.Fifo);
        var n2B = await n2.Get(b, mode: ConsistencyMode.Fifo);
        Assert.That(n2A.values.Concat(n2B.values), Values.EquivalentTo(V0, V0));

        await c.RunBackgroundTasksOnce();

        // N2: after anti-entropy, we expect the latest values a=1, b=2
        n2A = await n2.Get(a, mode: ConsistencyMode.Fifo);
        n2B = await n2.Get(b, mode: ConsistencyMode.Fifo);
        Assert.That(n2A.values.Concat(n2B.values), Values.EquivalentTo(V1, V2));
    }

    [Test]
    public async Task TestFifoDelete()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        // N1: initialize a=0, b=0
        await n1.Put(a, V0);
        await n1.Put(b, V0);
        
        // lose delete of a, then replicate delete of b
        await n1.Delete(a, mode: ReplicationMode.None);
        await n1.Delete(b, mode: ReplicationMode.Sync);
        var n1A = await n1.Get(a);
        var n1B = await n2.Get(b);
        Assert.That(n1A.values.Concat(n1B.values), Values.EquivalentTo(Value.None, Value.None));
        
        // FIFO consistency: we expect to see a=- BEFORE b=-, never after
        // N2: before anti-entropy, we expect the initial values a=0, b=0
        var n2A = await n2.Get(a, mode: ConsistencyMode.Fifo);
        var n2B = await n2.Get(b, mode: ConsistencyMode.Fifo);
        Assert.That(n2A.values.Concat(n2B.values), Values.EquivalentTo(V0, V0));
        
        await c.RunBackgroundTasksOnce();
        
        // N2: after anti-entropy, we expect the latest values a=-, b=-
        n2A = await n2.Get(a, mode: ConsistencyMode.Fifo);
        n2B = await n2.Get(b, mode: ConsistencyMode.Fifo);
        Assert.That(n2A.values.Concat(n2B.values), Is.Empty);
    }

    [Test]
    public async Task TestFifoOrder()
    {
        var c = new LocalNodeCluster(4);
        var nodes = c.ToArray();
        var rand = new Random(17);
        var lastValue = c.ToDictionary(n => n, _ => 0);
        var firstNode = c.GetClientApi(nodes.First());

        for (var i = 0; i < 32; i++)
        {
            // write strictly incrementing value on 4 different keys, in random node order
            var n = nodes[1 + rand.Next(nodes.Length - 1)];
            var m = rand.Next(4) == 0 ? ReplicationMode.Sync : ReplicationMode.None;
            var value = lastValue[n] = lastValue[n] + 1;
            var key = new Key($"{n}-{i % 4}");
            await c.GetClientApi(n).Put(key, value, mode: m);

            // FIFO consistency: we expect to never see a nodes' writes out of order
            var eventual = await firstNode.Get(key, mode: ConsistencyMode.Eventual);
            var fifo = await firstNode.Get(key, mode: ConsistencyMode.Fifo);
            
            // TODO: some assertion

            await c.RunBackgroundTasksOnce();
        }
    }
}