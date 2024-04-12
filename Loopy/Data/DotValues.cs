namespace Loopy.Data;

/// <summary>
/// Concurrent values
/// </summary>
public class DotValues : Map<Dot, Value>
{
    public DotValues()
    {
    }

    public DotValues(IDictionary<Dot, Value> dict) : base(dict)
    {
    }

    public override string ToString() => this.ValuesToString();
}