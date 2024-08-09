using Loopy.Core.Data;
using NUnit.Framework;

namespace Loopy.Test.Data;

public class FifoSegmentSetTests
{
    private int BitOr(int i1, int i2) => i1 | i2;

    [Test]
    public void TestPeekPop()
    {
        var s = new FifoSegmentSet<int>(BitOr);
        s.Add(new UpdateIdRange(5, 7), 1);
        s.Add(new UpdateIdRange(10, 12), 2);
        s.Add(new UpdateIdRange(0, 3), 4);
        TestContext.Out.WriteLine(s);

        Assert.That(s.Count, Is.EqualTo(3));
        Assert.That(s.PeekRange, Is.EqualTo(new UpdateIdRange(0, 3)));
        Assert.That(s.Pop().value, Is.EqualTo(4));
        Assert.That(s.PeekRange, Is.EqualTo(new UpdateIdRange(5, 7)));
        Assert.That(s.Pop().value, Is.EqualTo(1));
        Assert.That(s.PeekRange, Is.EqualTo(new UpdateIdRange(10, 12)));
        Assert.That(s.Pop().value, Is.EqualTo(2));
        Assert.That(s.Count, Is.EqualTo(0));
    }

    [Test]
    public void TestMergeAdjacent()
    {
        var s = new FifoSegmentSet<int>(BitOr);
        s.Add(new UpdateIdRange(0, 1), 1);
        s.Add(new UpdateIdRange(2, 4), 2);
        s.Add(new UpdateIdRange(5, 7), 4);
        s.Add(new UpdateIdRange(10, 15), 8);
        TestContext.Out.WriteLine(s);

        Assert.That(s.Count, Is.EqualTo(2));
        Assert.That(s.PeekRange, Is.EqualTo(new UpdateIdRange(0, 7)));
        Assert.That(s.Pop().value, Is.EqualTo(7));
        Assert.That(s.PeekRange, Is.EqualTo(new UpdateIdRange(10, 15)));
        Assert.That(s.Pop().value, Is.EqualTo(8));
        Assert.That(s.Count, Is.EqualTo(0));
    }

    [Test]
    public void TestMergeOverlapping()
    {
        var s = new FifoSegmentSet<int>(BitOr);
        s.Add(new UpdateIdRange(0, 3), 1);
        s.Add(new UpdateIdRange(2, 5), 2);
        TestContext.Out.WriteLine(s);

        Assert.That(s.Count, Is.EqualTo(1));
        Assert.That(s.PeekRange, Is.EqualTo(new UpdateIdRange(0, 5)));
        Assert.That(s.Pop().value, Is.EqualTo(3));
    }

    [Test]
    public void TestMergeContaining()
    {
        var s = new FifoSegmentSet<int>(BitOr);
        s.Add(new UpdateIdRange(0, 3), 1);
        s.Add(new UpdateIdRange(1, 2), 2);
        TestContext.Out.WriteLine(s);

        Assert.That(s.Count, Is.EqualTo(1));
        Assert.That(s.PeekRange, Is.EqualTo(new UpdateIdRange(0, 3)));
        Assert.That(s.Pop().value, Is.EqualTo(3));
    }
}
