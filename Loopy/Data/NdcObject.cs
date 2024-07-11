namespace Loopy.Data;

/// <summary>
/// An object internally encodes a logical clock by tagging
/// every (concurrent) value with a dot and storing all
/// current versions (versions) and past versions (causal context) as dots
/// </summary>
public class NdcObject
{
    public NdcObject() : this(null, null, null)
    { }

    public NdcObject(DotValues? vers, CausalContext? cc, Map<Dot, FifoDistances>? fd)
    {
        DotValues = vers != null ? new(vers) : new();
        CausalContext = cc != null ? new(cc) : new();
        FifoDistances = fd != null ? new(fd) : new();
    }

    public DotValues DotValues { get; init; }
    public CausalContext CausalContext { get; init; }
    public Map<Dot, FifoDistances> FifoDistances { get; init; }
    public bool IsEmpty => DotValues.All(kv => kv.Value.IsEmpty);
    public override string ToString() => $"{DotValues} / CC: {CausalContext} / FD: {FifoDistances.AsCsv()}";

    /// <summary>
    /// Merges the given object, i.e., add in new version values and remove obsoleted old version values
    /// </summary>
    internal NdcObject Merge(NdcObject other)
    {
        var o = new NdcObject();
        var o1 = this;
        var o2 = other;

        // versions of each object not obsoleted by each other
        // (d,v) obsoleted when (d,v) not in vers and dot is in cc
        o.DotValues.MergeIn(o1.DotValues.IntersectBy(o2.DotValues.Keys, p => p.Key));
        o.DotValues.MergeIn(o1.DotValues.Where(p => !o2.CausalContext.Contains(p.Key)));
        o.DotValues.MergeIn(o2.DotValues.Where(p => !o1.CausalContext.Contains(p.Key)));

        // same for fifo distances
        o.FifoDistances.MergeIn(o1.FifoDistances.IntersectBy(o2.FifoDistances.Keys, p => p.Key));
        o.FifoDistances.MergeIn(o1.FifoDistances.Where(p => !o2.CausalContext.Contains(p.Key)));
        o.FifoDistances.MergeIn(o2.FifoDistances.Where(p => !o1.CausalContext.Contains(p.Key)));

        // merged causal context: taking the maximum counter for common node ids
        o.CausalContext.MergeIn(o1.CausalContext);
        o.CausalContext.MergeIn(o2.CausalContext, (_, c1, c2) => Math.Max(c1, c2));

        return o;
    }

    /// <summary>
    /// Splits out only the given dot values into a new object
    /// </summary>
    internal NdcObject Split(IEnumerable<Dot> dots)
    {
        var s = new NdcObject();
        s.DotValues.MergeIn(dots.Select(d => (d, DotValues[d])));
        s.FifoDistances.MergeIn(dots.Select(d => (d, FifoDistances[d])));
        s.CausalContext.MergeIn(CausalContext);
        return s;
    }

    /// <summary>
    /// Removes per-key causal context that is already covered by the given node clock 
    /// </summary>
    internal NdcObject Strip(NodeClock nc)
    {
        var s = new NdcObject(DotValues, CausalContext, FifoDistances);

        foreach (var n in nc.Keys)
        {
            if (s.CausalContext[n] <= nc[n].Base)
                s.CausalContext.Remove(n);
        }

        return s;
    }

    /// <summary>
    /// Fills back per-key causal context from the given node clock
    /// </summary>
    internal NdcObject Fill(NodeClock nc, IEnumerable<NodeId> replicaNodes)
    {
        var f = new NdcObject(DotValues, CausalContext, FifoDistances);

        foreach (var n in replicaNodes)
            f.CausalContext[n] = Math.Max(f.CausalContext[n], nc[n].Base);

        return f;
    }
}
