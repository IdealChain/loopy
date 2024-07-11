using Loopy.Enums;
using System.Diagnostics;

namespace Loopy.Data;

/// <summary>
/// Preceeding FIFO update ID per priority level
/// </summary>
public class FifoDistances : Map<Priority, int>
{
    public FifoDistances()
    {
    }

    public FifoDistances(IEnumerable<KeyValuePair<Priority, int>> dists) : base(dists)
    {
        Trace.Assert(Values.All(v => v >= 1), "Invalid distance");
    }

    public FifoDistances(IEnumerable<KeyValuePair<Priority, int>> predecessors, int updateId) :
        this(predecessors.Select(kv => new KeyValuePair<Priority, int>(kv.Key, updateId - kv.Value)))
    {
    }

    /// <summary>
    /// Gets the update ID of the predecessor update for the given priority
    /// </summary>
    public int GetPredecessorId(Priority prio, int updateId)
    {
        if (TryGetValue(prio, out var dist))
        {
            Trace.Assert(updateId > dist, "Invalid updateId");
            return updateId - dist;
        }

        return 0;
    }

    /// <summary>
    /// Gets the range of update IDs this update can FIFO skip for the given priority
    /// </summary>
    public IEnumerable<int> GetSkippableUpdateIds(Priority prio, int updateId)
    {
        for (int i = GetPredecessorId(prio, updateId) + 1; i < updateId; i++)
            yield return i;
    }

    public override string ToString() => this.AsCsv();

    public FifoDistances Strip()
    {
        var s = new FifoDistances();
        var d = 1;
        
        // for equal distances, keep only first (lowest-prio) entry
        for (var p = Priority.P0; p <= Priority.P3; p++)
        {
            if (TryGetValue(p, out var c) && c > d)
                s[p] = d = c;
        }

        return s;
    }

    public FifoDistances Fill()
    {
        var f = new FifoDistances();
        var d = 1;

        // refill equal distances for omitted entries
        for (var p = Priority.P0; p <= Priority.P3; p++)
        {
            if (TryGetValue(p, out var c))
                d = Math.Max(d, c);

            f[p] = d;
        }

        return f;
    }
}
