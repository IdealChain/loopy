namespace Loopy.Data;

public readonly record struct Dot(NodeId NodeId, int UpdateId)
{
    public static implicit operator Dot((NodeId nodeId, int updateId) tuple) => new(tuple.nodeId, tuple.updateId);

    public override string ToString() => $"{NodeId}:{UpdateId}";
}