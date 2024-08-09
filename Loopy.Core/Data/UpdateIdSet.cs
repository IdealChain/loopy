using System.Text;

namespace Loopy.Core.Data;

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

    public UpdateIdSet(int @base, IEnumerable<int> bitmap)
    {
        _base = @base;
        UnionWith(bitmap);
    }

    public UpdateIdSet(params int[] ids)
    {
        UnionWith(ids);
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
        if (updateId > Base && _bitmap.Add(updateId))
            Normalize();
    }

    public void UnionWith(UpdateIdSet other)
    {
        _base = Math.Max(_base, other._base);
        _bitmap.UnionWith(other._bitmap);
        Normalize();
    }

    public void UnionWith(IEnumerable<int> other)
    {
        _bitmap.UnionWith(other);
        Normalize();
    }

    private void Normalize()
    {
        // remove bitmap values already covered by base
        while (_bitmap.Count > 0 && _bitmap.Min <= _base)
            _bitmap.Remove(_bitmap.Min);
        
        // advance base value as far as possible
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
