using Loopy.Data;
using Loopy.Interfaces;
using ProtoBuf;

namespace Loopy.Test.LocalCluster;

public class StorageTests
{
    [Test]
    public async Task TestPutGet()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        var r1 = await n1.Get("a");
        Assert.That(r1.values, Is.Empty);

        await n1.Put("a", "value", r1.cc);

        var r2 = await n1.Get("a");
        Assert.That(r2.values, Values.EquivalentTo("value"));

        var r3 = await n2.Get("a");
        Assert.That(r3.values, Values.EquivalentTo("value"));

        await n1.Delete("a", r2.cc);
        var r4 = await n1.Get("a");
        Assert.That(r4.values, Values.Empty());

        var r5 = await n2.Get("b");
        Assert.That(r5.values, Values.Empty());
    }

    [Test]
    public async Task TestGetConcurrent()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        var p1 = n1.Put("a", 1, CausalContext.Initial);
        var p2 = n2.Put("a", 2, CausalContext.Initial);
        await Task.WhenAll(p1, p2);
        
        var r1 = await n1.Get("a", 2);
        var r2 = await n2.Get("a", 2);
        Assert.Multiple(() =>
        {
            Assert.That(r1.values, Values.EquivalentTo(1, 2));
            Assert.That(r2.values, Values.EquivalentTo(1, 2));
        });

        // seen it, now resolve conflict with 3
        await n1.Put("a", 3, r1.cc);
        r1 = await n1.Get("a", 1);
        r2 = await n2.Get("a", 1);
        Assert.Multiple(() =>
        {
            Assert.That(r1.values, Values.EquivalentTo(3));
            Assert.That(r2.values, Values.EquivalentTo(3));
        });
        
        // put new value without context
        await n1.Put("a", 4, CausalContext.Initial);
        r1 = await n1.Get("a", 1);
        r2 = await n2.Get("a", 1);
        Assert.Multiple(() =>
        {
            Assert.That(r1.values, Values.EquivalentTo(4));
            Assert.That(r2.values, Values.EquivalentTo(4));
        });
    }
}
