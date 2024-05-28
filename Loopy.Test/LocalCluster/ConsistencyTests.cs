using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;

namespace Loopy.Test.LocalCluster;

public class ConsistencyTests
{
    private readonly Key a = "a";
    private readonly Key b = "b";
    private readonly Value v0 = 0;
    private readonly Value v1 = 1;
    private readonly Value v2 = 2;
    private readonly Value v3 = 3;

    [Test]
    public async Task TestFifoGet()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        // N1: initialize a=0, b=0
        await n1.Put(a, v0);
        await n1.Put(b, v0);

        // lose write of a=1, then replicate b=2
        await n1.Put(a, v1, mode: ReplicationMode.None);
        await n1.Put(b, v2, mode: ReplicationMode.Sync);
        
        var n1A = await n1.Get(a);
        var n1B = await n1.Get(b);
        Assert.That(n1A.values.Concat(n1B.values), Values.EquivalentTo(v1, v2));

        // FIFO consistency: we expect to see a=1 BEFORE b=2, never after
        // N2: before anti-entropy, we expect the initial values a=0, b=0
        var n2A = await n2.Get(a, mode: ConsistencyMode.Fifo);
        var n2B = await n2.Get(b, mode: ConsistencyMode.Fifo);
        Assert.That(n2A.values.Concat(n2B.values), Values.EquivalentTo(v0, v0));

        await c.RunBackgroundTasksOnce();

        // N2: after anti-entropy, we expect the latest values a=1, b=2
        n2A = await n2.Get(a, mode: ConsistencyMode.Fifo);
        n2B = await n2.Get(b, mode: ConsistencyMode.Fifo);
        Assert.That(n2A.values.Concat(n2B.values), Values.EquivalentTo(v1, v2));
    }

    [Test]
    public async Task TestFifoDelete()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        // N1: initialize a=0, b=0
        await n1.Put(a, v0);
        await n1.Put(b, v0);
        
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
        Assert.That(n2A.values.Concat(n2B.values), Values.EquivalentTo(v0, v0));
        
        await c.RunBackgroundTasksOnce();
        
        // N2: after anti-entropy, we expect the latest values a=-, b=-
        n2A = await n2.Get(a, mode: ConsistencyMode.Fifo);
        n2B = await n2.Get(b, mode: ConsistencyMode.Fifo);
        Assert.That(n2A.values.Concat(n2B.values), Is.Empty);
    }

    private readonly Key x = "P0_x";
    private readonly Key y = "P3_y";
    private readonly Key z = "P0_z";

    [Test]
    public async Task TestFifoPriority()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        // N1: initialize x=y=z=0
        await n1.Put(x, v0);
        await n1.Put(y, v0);
        await n1.Put(z, v0);

        // lose write of low prio x=1, then replicate high prio z=2 and low prio y=3
        await n1.Put(x, v1, mode: ReplicationMode.None);
        await n1.Put(y, v2, mode: ReplicationMode.Sync);
        await n1.Put(z, v3, mode: ReplicationMode.Sync);

        // Basic FIFO consistency:
        // - we expect to see x=1 BEFORE y=2 BEFORE z=3
        // - before anti-entropy, we expect the initial values x=y=z=0
        var n2X = await n2.Get(x, mode: ConsistencyMode.Fifo);
        var n2Y = await n2.Get(y, mode: ConsistencyMode.Fifo);
        var n2Z = await n2.Get(z, mode: ConsistencyMode.Fifo);
        Assert.That(n2X.values, Values.EqualTo(v0));
        Assert.That(n2Y.values, Values.EqualTo(v0));
        Assert.That(n2Z.values, Values.EqualTo(v0));

        // But with priority:
        // - we should see high prio y=2, since it DID arrive
        // - low-prio x=1 should block only z=3, NOT high prio y=2
        n2X = await n2.Get(x, mode: ConsistencyMode.FifoHigh);
        n2Y = await n2.Get(y, mode: ConsistencyMode.FifoHigh);
        n2Z = await n2.Get(z, mode: ConsistencyMode.FifoHigh);
        Assert.That(n2X.values, Values.EqualTo(v0));
        Assert.That(n2Y.values, Values.EqualTo(v2));
        Assert.That(n2Z.values, Values.EqualTo(v0));

        await c.RunBackgroundTasksOnce();

        // N2: after anti-entropy, we expect the latest values x=1, y=2, z=3
        n2X = await n2.Get(x, mode: ConsistencyMode.Fifo);
        n2Y = await n2.Get(y, mode: ConsistencyMode.Fifo);
        n2Z = await n2.Get(z, mode: ConsistencyMode.Fifo);
        Assert.That(n2X.values, Values.EqualTo(v1));
        Assert.That(n2Y.values, Values.EqualTo(v2));
        Assert.That(n2Z.values, Values.EqualTo(v3));
    }
}
