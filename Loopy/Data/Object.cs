namespace Loopy.Data;

/// <summary>
/// An object internally encodes a logical clock by tagging
/// every (concurrent) value with a dot and storing all
/// current versions (versions) and past versions (causal context) as dots
/// </summary>
public class Object
{
    public Object() : this(null, null, null)
    {
    }

    public Object(DotValues? vers, CausalContext? cc, Map<Dot, FifoDistances>? fd)
    {
        DotValues = vers != null ? new(vers) : new();
        CausalContext = cc != null ? new(cc) : new();
        FifoDistances = fd != null ? new(fd) : new();
    }

    public DotValues DotValues { get; init; }
    public CausalContext CausalContext { get; init; }
    public Map<Dot, FifoDistances> FifoDistances { get; init; }
    public bool IsEmpty => DotValues.All(kv => kv.Value.IsEmpty);
    public override string ToString() => $"{DotValues} / CC: {CausalContext} / FD: {FifoDistances.ValuesToString()}";
}
