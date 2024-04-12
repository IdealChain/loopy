using System.Text;

namespace Loopy.Data;

/// <summary>
/// Set of update ids / counter values, which stores the contigous range
/// [1-X] efficiently as one compact value and the remaining values individually  
/// </summary>
public class UpdateIdSet
{
    public UpdateIdSet()
    {
    }

    public UpdateIdSet(params int[] ids)
    {
        _bitmap = new(ids);
        Normalize();
    }

    private SortedSet<int> _bitmap = new();

    public int Base { get; private set; }

    public bool Contains(int updateId) => Base >= updateId || _bitmap.Contains(updateId);

    public IEnumerable<int> Except(UpdateIdSet other)
    {
        // other base less than our base => yield missing ids
        if (other.Base < Base)
        {
            foreach (var i in Enumerable.Range(other.Base + 1, Base - other.Base))
                yield return i;
        }

        // plus the difference in the bitmap set
        foreach (var i in _bitmap.Where(j => !other._bitmap.Contains(j)))
            yield return i;
    }

    public int Max => _bitmap.Count > 0 ? _bitmap.Max() : Base;

    public bool IsEmpty => Base == 0 && _bitmap.Count == 0;

    public void Add(int updateId)
    {
        if (updateId > Base && _bitmap.Add(updateId))
            Normalize();
    }

    public void UnionWith(UpdateIdSet other)
    {
        Base = Math.Max(Base, other.Base);
        _bitmap.UnionWith(other._bitmap);
        Normalize();
    }

    public void Normalize()
    {
        while (_bitmap.Remove(Base + 1))
            Base++;
    }

    public override string ToString()
    {
        var sb = new StringBuilder("(");

        if (!IsEmpty)
        {
            sb.Append(Base);
            sb.Append("; ");
            sb.Append(_bitmap.Count == 0 ? "-" : string.Join(",", _bitmap));
        }

        sb.Append(")");
        return sb.ToString();
    }
}