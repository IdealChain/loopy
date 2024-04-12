namespace Loopy.Data;

public readonly record struct NodeId(int Id)
{
    public static implicit operator NodeId(int id) => new(id);

    public override string ToString() => $"N{Id}";
}