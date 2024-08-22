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

    public static implicit operator Dictionary<int, int>(CausalContext cc) => cc.ToDictionary(kv => kv.Key.Id, kv => kv.Value);

    public static implicit operator CausalContext(Dictionary<int, int>? msg)
    {
        if (msg == null)
            return Initial;

        return new CausalContext(msg.Select(kv => new KeyValuePair<NodeId, int>(kv.Key, kv.Value)));
    }
}
