using Loopy.Core.Data;
using Loopy.Core.Enums;
using NUnit.Framework;

namespace Loopy.Core.Test.LocalCluster;

public class AntiEntropyTests
{
    private readonly Key a = "a";
    private readonly Key b = "b";
    private readonly Key c = "c";

    [Test]
    public async Task Test2Nodes()
    {
        var c = new LocalNodeCluster(2);
        var n1NR = c.GetClientApi(1, []);
        var n2NR = c.GetClientApi(2, []);

        await n1NR.Put(a, 1);
        await n1NR.Put(a, 2);
        await n2NR.Put(b, 7);

        Assert.That(await n2NR.GetValues(a), Values.Empty());

        await c[1].BackgroundTasks.AntiEntropy(2);
        await c[2].BackgroundTasks.AntiEntropy(1);

        Assert.That(await n2NR.GetValues(a), Values.EquivalentTo(2));
        Assert.That(await n1NR.GetValues(b), Values.EquivalentTo(7));
    }

    [Test]
    public async Task Test4Nodes()
    {
        var c = new LocalNodeCluster(4);
        foreach (var n in c)
        {
            var api = c.GetClientApi(n, []);
            await api.Put(n.ToString(), n.Id);
            await api.Put("key", n.Id);
        }

        // AE every node with every other node
        foreach(var (n1, n2) in c.SelectMany(n1 => c.Select(n2 => (n1, n2)).Where(t => t.n1 != t.n2)))
            await c[n1].BackgroundTasks.AntiEntropy(n2, CancellationToken.None);

        foreach (var n in c)
        {
            var api = c.GetClientApi(n, []);

            // key should conflict 1-4
            Assert.That(await api.GetValues("key"), Values.EquivalentTo(1, 2, 3, 4));

            // all node IDs should have propagated
            foreach (var m in c)
                Assert.That(await api.GetValues(m.ToString()), Values.EquivalentTo(m.Id));
        }
    }

    [Test]
    public async Task TestFifoSyncUp()
    {
        var cluster = new LocalNodeCluster(3);
        var n1 = cluster.GetClientApi(1);
        var n1R2 = cluster.GetClientApi(1, [2]);
        var n1NR = cluster.GetClientApi(1, []);
        var n3Fifo = cluster.GetClientApi(3, ConsistencyMode.Fifo);
        var n3Ev = cluster.GetClientApi(3, ConsistencyMode.Eventual);

        await n1.Put(a, 0);
        await n1.Put(b, 0);
        await n1.Put(a, 1);
        await n1R2.Put(b, 2); // (4) b=3 gets replicated to N2 only
        await n1.Put(c, 0);
        await n1.Put(a, 2);
        await n1.Put(c, 1);
        await n1NR.Put(a, 3); // (8) a=3 gets replicated nowhere, neither to N2 nor to N3
        await n1.Put(b, 3);
        await n1.Put(c, 2);

        // check N3 FIFO state: only updates 1-3 should be merged
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Fifo.GetValues(a), Values.EqualTo(1), a.Name);
            Assert.That(await n3Fifo.GetValues(b), Values.EqualTo(0), b.Name);
            Assert.That(await n3Fifo.GetValues(c), Values.Empty(), c.Name);
        });

        // check N3 eventual state: all updates except 4 and 8 should be merged
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Ev.GetValues(a), Values.EqualTo(2), a.Name);
            Assert.That(await n3Ev.GetValues(b), Values.EqualTo(3), b.Name);
            Assert.That(await n3Ev.GetValues(c), Values.EqualTo(2), c.Name);
        });

        // run N3 anti-entropy with N2: should receive update 4, but not 8
        await cluster[3].BackgroundTasks.AntiEntropy(2);

        // check N3 FIFO state: now, updates 1-7 should be merged
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Fifo.GetValues(a), Values.EqualTo(2), a.Name);
            Assert.That(await n3Fifo.GetValues(b), Values.EqualTo(2), b.Name);
            Assert.That(await n3Fifo.GetValues(c), Values.EqualTo(1), c.Name);
        });

        // check N3 eventual state: all updates except 8 should be merged
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Ev.GetValues(a), Values.EqualTo(2), a.Name);
            Assert.That(await n3Ev.GetValues(b), Values.EqualTo(3), b.Name);
            Assert.That(await n3Ev.GetValues(c), Values.EqualTo(2), c.Name);
        });

        // run N3 anti-entropy with N1: finally, with update 8, all updates should be visible
        await cluster[3].BackgroundTasks.AntiEntropy(1);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Fifo.GetValues(a), Values.EqualTo(3), a.Name);
            Assert.That(await n3Fifo.GetValues(b), Values.EqualTo(3), b.Name);
            Assert.That(await n3Fifo.GetValues(c), Values.EqualTo(2), c.Name);
        });

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Ev.GetValues(a), Values.EqualTo(3), a.Name);
            Assert.That(await n3Ev.GetValues(b), Values.EqualTo(3), b.Name);
            Assert.That(await n3Ev.GetValues(c), Values.EqualTo(2), c.Name);
        });

        cluster.SaveMaelstromHistory("fifo_syncup");
    }

    [Test]
    public async Task TestFifoSyncBuffers()
    {
        var cluster = new LocalNodeCluster(4);
        var n1 = cluster.GetClientApi(1);
        var n1R2 = cluster.GetClientApi(1, [2]);
        var n1R3 = cluster.GetClientApi(1, [3]);
        var n1R4 = cluster.GetClientApi(1, [4]);
        var n3Fifo = cluster.GetClientApi(3, ConsistencyMode.Fifo);
        var n3Ev = cluster.GetClientApi(3, ConsistencyMode.Eventual);
        var n4Fifo = cluster.GetClientApi(4, ConsistencyMode.Fifo);
        var n4Ev = cluster.GetClientApi(4, ConsistencyMode.Eventual);

        await n1.Put(a, 0);
        await n1.Put(b, 0);
        await n1.Put(c, 0);
        await n1R2.Put(a, 1); // (4) a=1 gets replicated to N2 only (gap on N3 and N4)
        await n1R3.Put(b, 1); // (5) b=1 gets replicated to N3 only
        await n1R4.Put(c, 1); // (6) c=1 gets replicated to N4 only

        // check N3/N4 FIFO state: only 1-3 should be visible
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Fifo.GetValues(a), Values.EqualTo(0), a.Name);
            Assert.That(await n3Fifo.GetValues(b), Values.EqualTo(0), b.Name);
            Assert.That(await n3Fifo.GetValues(c), Values.EqualTo(0), c.Name);
            Assert.That(await n4Fifo.GetValues(a), Values.EqualTo(0), a.Name);
            Assert.That(await n4Fifo.GetValues(b), Values.EqualTo(0), b.Name);
            Assert.That(await n4Fifo.GetValues(c), Values.EqualTo(0), c.Name);
        });

        // check N3/N4 eventual state: either 5 (N3) or 6 (N4) should be visible
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Ev.GetValues(a), Values.EqualTo(0), a.Name);
            Assert.That(await n3Ev.GetValues(b), Values.EqualTo(1), b.Name);
            Assert.That(await n3Ev.GetValues(c), Values.EqualTo(0), c.Name);
            Assert.That(await n4Ev.GetValues(a), Values.EqualTo(0), a.Name);
            Assert.That(await n4Ev.GetValues(b), Values.EqualTo(0), b.Name);
            Assert.That(await n4Ev.GetValues(c), Values.EqualTo(1), c.Name);
        });

        // run pair-wise anti-entropy between N3 and N4: buffered segments should be invisibly exchanged
        await cluster[3].BackgroundTasks.AntiEntropy(4);
        await cluster[4].BackgroundTasks.AntiEntropy(3);

        // check N3/N4 FIFO state: only 1-3 should be visible, same as before
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Fifo.GetValues(a), Values.EqualTo(0), a.Name);
            Assert.That(await n3Fifo.GetValues(b), Values.EqualTo(0), b.Name);
            Assert.That(await n3Fifo.GetValues(c), Values.EqualTo(0), c.Name);
            Assert.That(await n4Fifo.GetValues(a), Values.EqualTo(0), a.Name);
            Assert.That(await n4Fifo.GetValues(b), Values.EqualTo(0), b.Name);
            Assert.That(await n4Fifo.GetValues(c), Values.EqualTo(0), c.Name);
        });

        // check N3/N4 eventual state: 5 (N3) and 6 (N4) should be visible
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Ev.GetValues(a), Values.EqualTo(0), a.Name);
            Assert.That(await n3Ev.GetValues(b), Values.EqualTo(1), b.Name);
            Assert.That(await n3Ev.GetValues(c), Values.EqualTo(1), c.Name);
            Assert.That(await n4Ev.GetValues(a), Values.EqualTo(0), a.Name);
            Assert.That(await n4Ev.GetValues(b), Values.EqualTo(1), b.Name);
            Assert.That(await n4Ev.GetValues(c), Values.EqualTo(1), c.Name);
        });

        // run N3/N4 anti-entropy with N2: missing update 4 should be filled in
        await cluster[3].BackgroundTasks.AntiEntropy(2);
        await cluster[4].BackgroundTasks.AntiEntropy(2);

        // check N3/N4 FIFO state: with all updates being available, FIFO should be up to date
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Fifo.GetValues(a), Values.EqualTo(1), a.Name);
            Assert.That(await n3Fifo.GetValues(b), Values.EqualTo(1), b.Name);
            Assert.That(await n3Fifo.GetValues(c), Values.EqualTo(1), c.Name);
            Assert.That(await n4Fifo.GetValues(a), Values.EqualTo(1), a.Name);
            Assert.That(await n4Fifo.GetValues(b), Values.EqualTo(1), b.Name);
            Assert.That(await n4Fifo.GetValues(c), Values.EqualTo(1), c.Name);
        });

        // check N3/N4 eventual state:  with all updates being available, all should be up to date
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await n3Ev.GetValues(a), Values.EqualTo(1), a.Name);
            Assert.That(await n3Ev.GetValues(b), Values.EqualTo(1), b.Name);
            Assert.That(await n3Ev.GetValues(c), Values.EqualTo(1), c.Name);
            Assert.That(await n4Ev.GetValues(a), Values.EqualTo(1), a.Name);
            Assert.That(await n4Ev.GetValues(b), Values.EqualTo(1), b.Name);
            Assert.That(await n4Ev.GetValues(c), Values.EqualTo(1), c.Name);
        });

        cluster.SaveMaelstromHistory("fifo_syncbuffers");
    }
}
