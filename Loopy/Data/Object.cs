using Loopy.Enums;
using Loopy.Interfaces;

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

    public Object(DotValues? vers, CausalContext? cc, FifoBarriers? fb)
    {
        DotValues = vers != null ? new(vers) : new();
        CausalContext = cc != null ? new(cc) : new();
        FifoBarriers = fb != null ? new(fb) : new();
    }

    public DotValues DotValues { get; init; }
    public CausalContext CausalContext { get; init; }
    public FifoBarriers FifoBarriers { get; init; } // TODO: per dot

    public override string ToString() => $"{DotValues} / CC: {CausalContext} / FB: {FifoBarriers}";
}
