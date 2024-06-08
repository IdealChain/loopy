using System.Text;

namespace Loopy.Data;

/// <summary>
/// Set of update ids / counter values, which stores the contigous range
/// [1-X] efficiently as one compact value and the remaining values individually  
/// </summary>
public class UpdateIdSet
{
    private int _base;
    private SortedSet<int> _bitmap = new();

    public UpdateIdSet()
    {
    }

    public UpdateIdSet(params int[] ids)
    {
        _bitmap = new(ids);
        Normalize();
    }

    public int Base => _base;

    public IEnumerable<int> Bitmap => _bitmap;

    public bool Contains(int updateId) => updateId <= _base || _bitmap.Contains(updateId);

    public IEnumerable<int> Except(UpdateIdSet other)
    {
        // other base less than our base => yield missing ids
        if (other._base < _base)
        {
            foreach (var i in Enumerable.Range(other._base + 1, _base - other._base))
                if (!other.Contains(i))
                    yield return i;
        }

        // plus the difference in the bitmap set
        foreach (var i in _bitmap.Where(j => !other.Contains(j)))
            yield return i;
    }

    public int Max => _bitmap.Count > 0 ? _bitmap.Max() : _base;

    public bool IsEmpty => _base == 0 && _bitmap.Count == 0;

    public void Add(int updateId)
    {
        if (updateId > _base && _bitmap.Add(updateId))
            Normalize();
    }

    public void UnionWith(UpdateIdSet other)
    {
        _base = Math.Max(_base, other._base);
        _bitmap.UnionWith(other._bitmap);
        _bitmap.RemoveWhere(updateId => updateId <= _base);
        Normalize();
    }

    public void Normalize()
    {
        while (_bitmap.Remove(_base + 1))
            _base++;
    }

    public override string ToString()
    {
        var sb = new StringBuilder("(");

        if (!IsEmpty)
        {
            sb.Append(_base);
            sb.Append("; ");
            sb.Append(_bitmap.Count == 0 ? "-" : string.Join(",", _bitmap));
        }

        sb.Append(")");
        return sb.ToString();
    }
}
