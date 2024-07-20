namespace Loopy.Data;

/// <summary>
/// Concurrent values
/// </summary>
public class DotValues : Map<Dot, (Value value, int[] fifoDistances)>
{
    public DotValues()
    {
    }

    public DotValues(IEnumerable<KeyValuePair<Dot, (Value value, int[] fifoDistances)>> values) : base(values)
    {
    }

    public override string ToString() => this
        .Select(kv => $"{kv.Key}={kv.Value.value} (FD: {kv.Value.fifoDistances.AsCsv()})")
        .AsCsv();
}
