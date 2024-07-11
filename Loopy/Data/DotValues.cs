namespace Loopy.Data;

/// <summary>
/// Concurrent values
/// </summary>
public class DotValues : Map<Dot, Value>
{
    public DotValues()
    {
    }

    public DotValues(IEnumerable<KeyValuePair<Dot, Value>> dists) : base(dists)
    {
    }

    public override string ToString() => this.AsCsv();
}
