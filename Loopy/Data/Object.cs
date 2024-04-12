namespace Loopy.Data;

/// <summary>
/// An object internally encodes a logical clock by tagging
/// every (concurrent) value with a dot and storing all
/// current versions (versions) and past versions (causal context) as dots
/// </summary>
public class Object
{
    public Object() : this(null, null)
    {
    }

    public Object(DotValues? vers, CausalContext? cc)
    {
        DotValues = vers != null ? new(vers) : new();
        CausalContext = cc != null ? new(cc) : new();
    }

    public DotValues DotValues { get; init; }
    public CausalContext CausalContext { get; init; }

    public override string ToString() => $"{DotValues} / CC: {CausalContext}";
}