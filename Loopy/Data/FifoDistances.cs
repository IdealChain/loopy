using Loopy.Enums;

namespace Loopy.Data;

/// <summary>
/// Preceeding FIFO update ID per priority level
/// </summary>
public class FifoDistances : Map<Priority, int>
{
    public FifoDistances()
    {
    }

    public FifoDistances(IEnumerable<KeyValuePair<Priority, int>> dict) : base(dict)
    {
    }

    public override string ToString() => this.ValuesToString();

    public FifoDistances Strip()
    {
        var s = new FifoDistances();
        var d = 1;

        // for equal distances, keep only first (lowest-prio) entry
        for (var p = Priority.Bulk; p <= Priority.High; p++)
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
        for (var p = Priority.Bulk; p <= Priority.High; p++)
        {
            if (TryGetValue(p, out var c))
                d = Math.Max(d, c);

            f[p] = d;
        }

        return f;
    }
}
