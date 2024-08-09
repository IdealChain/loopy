namespace Loopy.Core.Data;

/// <summary>
/// Causal context: past versions
/// </summary>
public class CausalContext : Map<NodeId, int>
{
    public CausalContext()
    { }

    public CausalContext(IEnumerable<KeyValuePair<NodeId, int>> cc) : base(cc)
    { }

    public static CausalContext Initial => new();

    public bool Contains(Dot dot) => dot.UpdateId <= this[dot.NodeId];

    public override string ToString() => this.AsCsv(kv => $"{kv.Key}={kv.Value}");
}
