using Loopy.Core.Enums;

namespace Loopy.Core.Data;

/// <summary>
/// Contiguous range of update IDs
/// </summary>
public readonly struct UpdateIdRange : IEquatable<UpdateIdRange>
{
    public int First { get; }
    public int Last { get; }

    public UpdateIdRange(int updateId, Priority prio, int[] fifoDistances)
        : this(fifoDistances.GetFifoPredecessor(updateId, prio) + 1, updateId)
    {
    }

    public UpdateIdRange(int first, int last)
    {
        if (last < first)
            throw new ArgumentOutOfRangeException(nameof(last), "Last update ID must be equal or larger");

        First = first;
        Last = last;
    }

    public bool Contains(UpdateIdRange other) => First <= other.First && other.Last <= Last;
    public bool Overlaps(UpdateIdRange other) => First <= other.Last && other.First <= Last;
    public bool AdjacentTo(UpdateIdRange other) => Distance(other) == 0;
    public bool HasGapTo(UpdateIdRange other) => Distance(other) > 0;

    /// <summary>
    /// Gets the number of update IDs between this range and the given one -
    /// zero if they touch each other, null if they overlap
    /// </summary>
    public int? Distance(UpdateIdRange other)
    {
        if (Overlaps(other))
            return null;

        return checked(other.First > First ? other.First - Last - 1 : First - other.Last - 1);
    }

    /// <summary>
    /// Returns the combined range both this range and the given one cover
    /// </summary>
    public UpdateIdRange Union(UpdateIdRange other)
    {
        if (Distance(other).GetValueOrDefault() > 0)
            throw new InvalidOperationException("Cannot combine gapped ranges");

        return new UpdateIdRange(Math.Min(First, other.First), Math.Max(Last, other.Last));
    }

    public override string ToString() => $"[{First}-{Last}]";

    #region IEquatable

    public bool Equals(UpdateIdRange other) => First == other.First && Last == other.Last;
    public override bool Equals(object? obj) => obj is UpdateIdRange other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(First, Last);

    #endregion
}
