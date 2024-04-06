using System.Text;

namespace Loopy;

public readonly record struct Key(string Name)
{
    public static implicit operator Key(string name) => new(name);

    public override string ToString() => Name;
}

public readonly record struct Value(string? Data)
{
    public bool IsEmpty => string.IsNullOrEmpty(Data);

    public static Value Null => new(null);

    public static implicit operator Value(string data) => new(data);

    public static implicit operator Value(int data) => new(Convert.ToString(data));

    public override string ToString() => Data ?? "-";
}

public readonly record struct NodeId(int Id)
{
    public static implicit operator NodeId(int id) => new(id);

    public override string ToString() => $"N{Id}";
}

public readonly record struct Dot(NodeId NodeId, int UpdateId)
{
    public static implicit operator Dot((NodeId nodeId, int updateId) tuple) => new(tuple.nodeId, tuple.updateId);

    public override string ToString() => $"{NodeId}:{UpdateId}";
}

public record CausalContext(Dictionary<NodeId, int> cc)
{
    public static CausalContext Initial => new(new Dictionary<NodeId, int>());

    public override string ToString()
    {
        var sb = new StringBuilder("CC:");

        foreach (var (n, c) in cc)
            sb.AppendFormat(" {0}={1}", n, c);

        return sb.ToString();
    }
}

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
        // other base < our base => yield missing ids
        foreach (var i in Enumerable.Range(other.Base + 1, Math.Max(0, Base - other.Base)))
            yield return i;

        // plus bitmap difference
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