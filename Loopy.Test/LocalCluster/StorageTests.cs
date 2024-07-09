using Loopy.Data;
using NUnit.Framework;

namespace Loopy.Test.LocalCluster;

public class StorageTests
{
    private readonly Key a = "a";
    private readonly Key b = "b";
    private readonly Key c = "c";

    [Test]
    public async Task TestPutGetValues()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);
        var cc = CausalContext.Initial;

        Assert.That(await n1.GetValues(a), Is.Empty);

        cc = await n1.GetCC(a);
        await n1.Put(a, "value", cc);

        Assert.That(await n1.GetValues(a), Values.EquivalentTo("value"));
        Assert.That(await n2.GetValues(a), Values.EquivalentTo("value"));

        cc = await n1.GetCC(a);
        await n1.Delete(a, cc);

        Assert.That(await n1.GetValues(a), Values.Empty());
        Assert.That(await n2.GetValues(b), Values.Empty());
    }

    [Test]
    public async Task TestGetConcurrent()
    {
        var c = new LocalNodeCluster(2);
        var n1 = c.GetClientApi(1);
        var n2 = c.GetClientApi(2);

        var p1 = n1.Put(a, 1, CausalContext.Initial);
        var p2 = n2.Put(a, 2, CausalContext.Initial);
        await Task.WhenAll(p1, p2);
        
        Assert.Multiple(async () =>
        {
            Assert.That(await n1.GetValues(a), Values.EquivalentTo(1, 2));
            Assert.That(await n2.GetValues(a), Values.EquivalentTo(1, 2));
        });

        // seen it, now resolve conflict with 3
        await n1.Put(a, 3, (await n1.GetCC(a)));
        Assert.Multiple(async () =>
        {
            Assert.That(await n1.GetValues(a), Values.EquivalentTo(3));
            Assert.That(await n2.GetValues(a), Values.EquivalentTo(3));
        });
        
        // put new value without context
        await n1.Put(a, 4, CausalContext.Initial);
        Assert.Multiple(async () =>
        {
            Assert.That(await n1.GetValues(a), Values.EquivalentTo(4));
            Assert.That(await n2.GetValues(a), Values.EquivalentTo(4));
        });
    }
}
