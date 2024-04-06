namespace Loopy.Test;

public class DataTests
{
    [Test]
    public void TestUpdateIdSetAdd()
    {
        var set = new UpdateIdSet();
        Assert.That(set.IsEmpty);
        Assert.That(set.Contains(1), Is.False);
        Assert.That(set.Base, Is.EqualTo(0));

        set.Add(1);
        Assert.That(set.IsEmpty, Is.False);
        Assert.That(set.Contains(1), Is.True);
        Assert.That(set.Contains(2), Is.False);
        Assert.That(set.Base, Is.EqualTo(1));

        set.Add(3);
        Assert.That(set.Contains(1), Is.True);
        Assert.That(set.Contains(2), Is.False);
        Assert.That(set.Contains(3), Is.True);
        Assert.That(set.Base, Is.EqualTo(1));

        set.Add(2);
        Assert.That(set.Contains(1), Is.True);
        Assert.That(set.Contains(2), Is.True);
        Assert.That(set.Contains(3), Is.True);
        Assert.That(set.Base, Is.EqualTo(3));
    }

    [Test]
    public void TestUpdateIdSetUnionWith()
    {
        var s1 = new UpdateIdSet(1, 2, 3, 7, 8);
        Assert.That(s1.Base, Is.EqualTo(3));
        Assert.That(s1.Max, Is.EqualTo(8));

        var s2 = new UpdateIdSet(1, 2, 3, 4, 10, 12);
        Assert.That(s2.Base, Is.EqualTo(4));
        Assert.That(s2.Max, Is.EqualTo(12));

        s1.UnionWith(s2);
        Assert.That(s1.Base, Is.EqualTo(4));
        Assert.That(s1.Contains(7));
        Assert.That(s1.Contains(8));
        Assert.That(s1.Contains(10));
        Assert.That(s1.Contains(12));
    }

    [Test]
    public void TestUpdateIdSetExcept()
    {
        var s1 = new UpdateIdSet(1, 2, 3, 4);
        var s2 = new UpdateIdSet(1, 2);
        Assert.That(s1.Except(s2), Is.EquivalentTo(new[] { 3, 4 }));
        Assert.That(s2.Except(s1), Is.Empty);

        var s3 = new UpdateIdSet(4, 7, 9);
        var s4 = new UpdateIdSet(7, 8, 9);
        Assert.That(s3.Except(s4), Is.EquivalentTo(new[] { 4 }));
        Assert.That(s4.Except(s3), Is.EquivalentTo(new[] { 8 }));

        var s5 = new UpdateIdSet(1, 2, 3, 7, 8);
        var s6 = new UpdateIdSet(1, 2, 3, 4, 10, 12);
        Assert.That(s5.Except(s6), Is.EquivalentTo(new[] { 7, 8 }));
        Assert.That(s6.Except(s5), Is.EquivalentTo(new[] { 4, 10, 12 }));
    }

    [Test]
    public void TestMergeInUnique()
    {
        var d = new Dictionary<int, int>();
        d.MergeIn(new[] { (1, 1) });
        d.MergeIn(new[] { (3, 5) });
        Assert.That(d, Does.ContainKey(1).WithValue(1));
        Assert.That(d, Does.ContainKey(3).WithValue(5));
    }

    [Test]
    public void TestMergeInEqual()
    {
        var d = new Dictionary<int, int>();
        d.MergeIn(new[] { (3, 5) });
        d.MergeIn(new[] { (3, 5) });
        Assert.That(d, Does.ContainKey(3).WithValue(5));
    }

    [Test]
    public void TestMergeInConflict()
    {
        int MaxResolver(int k, int v1, int v2)
        {
            var r = Math.Max(v1, v2);
            TestContext.Out.WriteLine($"Resolving {k} conflict with {r} ({v1}:{v2})");
            return r;
        }

        var d = new Dictionary<int, int>();
        d.MergeIn(new[] { (3, 5) });
        Assert.That(d, Does.ContainKey(3).WithValue(5));

        // without conflict resolver: exception
        Assert.Throws<InvalidOperationException>(() => d.MergeIn(new[] { (3, 3) }));
        Assert.That(d, Does.ContainKey(3).WithValue(5));

        // with conflict resolver: resolving
        d.MergeIn(new[] { (3, 3) }, MaxResolver);
        Assert.That(d, Does.ContainKey(3).WithValue(5));

        d.MergeIn(new[] { (3, 8) }, MaxResolver);
        Assert.That(d, Does.ContainKey(3).WithValue(8));
    }

    [Test]
    public void TestContainsDot()
    {
        var cc = new SafeDict<NodeId, int>();
        Dot dot = (13, 2);
        Assert.That(cc.ContainsDot(dot), Is.False);

        cc[dot.NodeId] = 1;
        Assert.That(cc.ContainsDot(dot), Is.False);

        cc[dot.NodeId] = 2;
        Assert.That(cc.ContainsDot(dot), Is.True);

        cc[dot.NodeId] = 4;
        Assert.That(cc.ContainsDot(dot), Is.True);
    }
}