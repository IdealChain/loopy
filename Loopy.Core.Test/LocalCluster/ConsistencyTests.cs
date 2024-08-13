using Loopy.Core.Data;
using Loopy.Core.Enums;
using NUnit.Framework;

namespace Loopy.Core.Test.LocalCluster;

public class ConsistencyTests
{
    private readonly Key a = "a";
    private readonly Key b = "b";
    private readonly Key c = "c";

    [Test]
    public async Task TestFifoGet()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1, ConsistencyMode.Fifo);
        var n1NR = c.GetClientApi(1, ConsistencyMode.Fifo, []);
        var n2 = c.GetClientApi(2, ConsistencyMode.Fifo);

        // N1: initialize a=0, b=0
        await n1.Put(a, 0);
        await n1.Put(b, 0);

        // lose write of a=1, then replicate b=2
        await n1NR.Put(a, 1);
        await n1.Put(b, 2);

        Assert.That(await n1.GetValues(a), Values.EqualTo(1));
        Assert.That(await n1.GetValues(b), Values.EqualTo(2));

        // FIFO consistency: we expect to see a=1 BEFORE b=2, never after
        // N2: before anti-entropy, we expect the initial values a=0, b=0
        Assert.That(await n2.GetValues(a), Values.EqualTo(0));
        Assert.That(await n2.GetValues(b), Values.EqualTo(0));

        await c.GetBackgroundTasks(2).AntiEntropy(1);

        // N2: after anti-entropy, we expect the latest values a=1, b=2
        Assert.That(await n2.GetValues(a), Values.EqualTo(1));
        Assert.That(await n2.GetValues(b), Values.EqualTo(2));
    }

    [Test]
    public async Task TestFifoDelete()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1, ConsistencyMode.Fifo);
        var n1NR = c.GetClientApi(1, ConsistencyMode.Fifo, []);
        var n2 = c.GetClientApi(2, ConsistencyMode.Fifo);

        // N1: initialize a=0, b=0
        await n1.Put(a, 0);
        await n1.Put(b, 0);
        
        // lose delete of a, then replicate delete of b
        await n1NR.Delete(a);
        await n1.Delete(b);
        Assert.That(await n1.GetValues(a), Values.Empty());
        Assert.That(await n1.GetValues(b), Values.Empty());

        // FIFO consistency: we expect to see a=- BEFORE b=-, never after
        // N2: before anti-entropy, we expect the initial values a=0, b=0
        Assert.That(await n2.GetValues(a), Values.EqualTo(0));
        Assert.That(await n2.GetValues(b), Values.EqualTo(0));

        await c.GetBackgroundTasks(2).AntiEntropy(1);

        // N2: after anti-entropy, we expect the latest values a=-, b=-
        Assert.That(await n2.GetValues(a), Values.Empty());
        Assert.That(await n2.GetValues(b), Values.Empty());
    }

    private readonly Key x = "P0_x";
    private readonly Key y = "P3_y";
    private readonly Key z = "P0_z";

    [Test]
    public async Task TestFifoPriority()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1, ConsistencyMode.Fifo);
        var n1NR = c.GetClientApi(1, ConsistencyMode.Fifo, []);
        var n2All = c.GetClientApi(2, ConsistencyMode.Fifo);
        var n2High = c.GetClientApi(2, ConsistencyMode.FifoP3);

        // N1: initialize x=y=z=0
        await n1.Put(x, 0);
        await n1.Put(y, 0);
        await n1.Put(z, 0);

        // lose write of low prio x=1, then replicate high prio z=2 and low prio y=3
        await n1NR.Put(x, 1);
        await n1.Put(y, 2);
        await n1.Put(z, 3);

        // Basic FIFO consistency:
        // - we expect to see x=1 BEFORE y=2 BEFORE z=3
        // - before anti-entropy, we expect the initial values x=y=z=0
        Assert.That(await n2All.GetValues(x), Values.EqualTo(0));
        Assert.That(await n2All.GetValues(y), Values.EqualTo(0));
        Assert.That(await n2All.GetValues(z), Values.EqualTo(0));

        // But with high priority:
        // - we should see high prio y=2, since it DID arrive
        // - we should not see anything of low-prio x, z
        Assert.That(await n2High.GetValues(x), Values.Empty());
        Assert.That(await n2High.GetValues(y), Values.EqualTo(2));
        Assert.That(await n2High.GetValues(z), Values.Empty());

        await c.GetBackgroundTasks(2).AntiEntropy(1);

        // Basic FIFO: after anti-entropy, we expect the latest values x=1, y=2, z=3
        Assert.That(await n2All.GetValues(x), Values.EqualTo(1));
        Assert.That(await n2All.GetValues(y), Values.EqualTo(2));
        Assert.That(await n2All.GetValues(z), Values.EqualTo(3));
        
        // High prio results should not have changed
        Assert.That(await n2High.GetValues(x), Values.Empty());
        Assert.That(await n2High.GetValues(y), Values.EqualTo(2));
        Assert.That(await n2High.GetValues(z), Values.Empty());
    }
}
