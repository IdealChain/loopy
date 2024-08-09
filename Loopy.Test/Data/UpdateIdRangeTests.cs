using Loopy.Core.Data;
using Loopy.Core.Enums;
using NUnit.Framework;

namespace Loopy.Test.Data;

public class UpdateIdRangeTests
{
    [Test]
    public void TestRange()
    {
        var i = new UpdateIdRange(5, 10);
        Assert.That(i.First, Is.EqualTo(5));
        Assert.That(i.Last, Is.EqualTo(10));

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new UpdateIdRange(6, 5));
    }

    [Test]
    public void TestDistances()
    {
        var i = new UpdateIdRange(10, Priority.P2, [1, 3, 5, 7]);
        Assert.That(i.First, Is.EqualTo(6));
        Assert.That(i.Last, Is.EqualTo(10));
    }

    [Test]
    public void TestContains()
    {
        var i1 = new UpdateIdRange(5, 5);
        Assert.That(i1.Contains(new UpdateIdRange(5, 5)), Is.True);
        Assert.That(new UpdateIdRange(3, 5).Contains(i1), Is.True);
        Assert.That(new UpdateIdRange(3, 4).Contains(i1), Is.False);

        var i2 = new UpdateIdRange(5, 10);
        Assert.That(i2.Contains(i1), Is.True);
        Assert.That(i2.Contains(new UpdateIdRange(6, 8)), Is.True);
        Assert.That(i2.Contains(new UpdateIdRange(6, 11)), Is.False);
    }

    [Test]
    public void TestOverlaps()
    {
        var i1 = new UpdateIdRange(5, 10);
        var i2 = new UpdateIdRange(8, 8);
        var i3 = new UpdateIdRange(10, 15);
        var i4 = new UpdateIdRange(15, 20);

        Assert.That(i1.Overlaps(i2), Is.True);
        Assert.That(i1.Overlaps(i3), Is.True);
        Assert.That(i1.Overlaps(i4), Is.False);
        Assert.That(i2.Overlaps(i1), Is.True);
        Assert.That(i2.Overlaps(i3), Is.False);
        Assert.That(i2.Overlaps(i4), Is.False);
        Assert.That(i3.Overlaps(i4), Is.True);
    }

    [Test]
    public void TestDistance()
    {
        var i1 = new UpdateIdRange(5, 5);
        var i2 = new UpdateIdRange(8, 10);
        var i3 = new UpdateIdRange(9, 15);

        Assert.That(i1.Distance(i2), Is.EqualTo(2));
        Assert.That(i1.Distance(i3), Is.EqualTo(3));
        Assert.That(i2.Distance(i3), Is.Null);
    }

    [Test]
    public void TestUnion()
    {
        var i1 = new UpdateIdRange(5, 5);
        var i2 = new UpdateIdRange(8, 10);
        var i3 = new UpdateIdRange(9, 15);

        Assert.That(i1.Union(i1), Is.EqualTo(i1));
        Assert.That(i2.Union(i3), Is.EqualTo(new UpdateIdRange(8, 15)));

        Assert.Throws<InvalidOperationException>(() => i1.Union(i2));
    }
}
