using System.Collections;
using System.Diagnostics;

namespace Loopy.Data;

/// <summary>
/// Sorted set of contiguous, mergable update ID range segments
/// </summary>
public class FifoSegmentSet<T> : IEnumerable<(UpdateIdRange range, T value)>
{
    /// <summary>
    /// Segments sorted by increasing interval start value
    /// (an interval tree could provide better insertion/removal efficiency)
    /// </summary>
    private readonly List<(UpdateIdRange range, T value)> _segments = new();
    private readonly Func<T, T, T> _mergeFunc;
    private static readonly SegmentOrderComparer _segmentComparer = new();

    public class SegmentOrderComparer : Comparer<(UpdateIdRange range, T value)>
    {
        public override int Compare((UpdateIdRange range, T value) x, (UpdateIdRange range, T value) y)
        {
            return x.range.First.CompareTo(y.range.First);
        }
    }

    public FifoSegmentSet(Func<T, T, T> mergeFunc)
    {
        _mergeFunc = mergeFunc;
    }

    /// <summary>
    /// Gets the range of the first segment
    /// </summary>
    public UpdateIdRange PeekRange => _segments[0].range;

    /// <summary>
    /// Removes and returns the first segment
    /// </summary>
    public (UpdateIdRange range, T value) Pop()
    {
        var min = _segments[0];
        _segments.RemoveAt(0);
        return min;
    }

    public void Add(UpdateIdRange range, T value)
    {
        // find position of exact or next larger range start match
        var i = _segments.BinarySearch((range, value), _segmentComparer);
        var sortPos = i >= 0 ? i : ~i;

        // check whether new value can be merged into neighboring ranges
        if (sortPos - 1 >= 0 && !range.HasGapTo(_segments[sortPos - 1].range))
            Merge(sortPos - 1, range, value);
        else if (sortPos < _segments.Count && !range.HasGapTo(_segments[sortPos].range))
            Merge(sortPos, range, value);
        else
            Insert(sortPos, range, value);
    }

    private void Merge(int i, UpdateIdRange range, T value)
    {
        // sanity check that merge is compatible and neighboring ranges are not overlapped
        Debug.Assert(range.Overlaps(_segments[i].range) || range.AdjacentTo(_segments[i].range));
        Debug.Assert(i - 1 < 0 || _segments[i - 1].range.Last < range.First);
        Debug.Assert(i + 1 >= _segments.Count || _segments[i + 1].range.First > range.Last);

        var existing = _segments[i];
        _segments[i] = (existing.range.Union(range), _mergeFunc(existing.value, value));
    }

    private void Insert(int i, UpdateIdRange range, T value)
    {
        // sanity check that neighboring ranges are not overlapped
        Debug.Assert(i - 1 < 0 || _segments[i - 1].range.Last < range.First);
        Debug.Assert(i + 1 >= _segments.Count || _segments[i + 1].range.First > range.Last);

        _segments.Insert(i, (range, value));
    }

    public int Count => _segments.Count;

    public override string ToString() => _segments.AsCsv(s => s.range.ToString());

    public IEnumerator<(UpdateIdRange range, T value)> GetEnumerator() => _segments.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _segments.GetEnumerator();
}
