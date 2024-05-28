using Loopy.Enums;

namespace Loopy.Data;

/// <summary>
/// Preceeding FIFO update ID per priority level
/// </summary>
public class FifoBarriers : Map<Priority, int>
{
    public FifoBarriers()
    {
    }

    public FifoBarriers(IEnumerable<KeyValuePair<Priority, int>> dict) : base(dict)
    {
    }

    public override string ToString() => this.ValuesToString();
}
