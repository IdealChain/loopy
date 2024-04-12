using Loopy.Data;

namespace Loopy.Test.Data;

public class DictionaryExtensionsTests
{
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
}