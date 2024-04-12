using Loopy.Data;

namespace Loopy.Test.Data;

public class CausalContextTests
{
    [Test]
    public void TestCausalContextContainsDot()
    {
        var cc = new CausalContext();
        Dot dot = (13, 2);
        Assert.That(cc.Contains(dot), Is.False);

        cc[dot.NodeId] = 1;
        Assert.That(cc.Contains(dot), Is.False);

        cc[dot.NodeId] = 2;
        Assert.That(cc.Contains(dot), Is.True);

        cc[dot.NodeId] = 4;
        Assert.That(cc.Contains(dot), Is.True);
    }
}