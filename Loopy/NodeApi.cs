namespace Loopy;

/// <summary>
/// Node API for other nodes to fetch or push objects
/// </summary>
public interface INodeApi
{
    Task<Object> Fetch(Key k);
    
    Task<Object> Update(Key k, Object o);

    Task<(SafeDict<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncClock(
        NodeId p, SafeDict<NodeId, UpdateIdSet> nodeClockP);
}

public class NodeApi : INodeApi
{
    private readonly Node _node;

    public NodeApi(Node node)
    {
        _node = node;
    }

    public async Task<Object> Fetch(Key k)
    {
        await Task.Delay(10);
        return _node.Fetch(k);
    }

    public async Task<Object> Update(Key k, Object o)
    {
        await Task.Delay(25);
        return _node.Update(k, o);
    }

    public async Task<(SafeDict<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncClock(
        NodeId p, SafeDict<NodeId, UpdateIdSet> nodeClockP)
    {
        await Task.Delay(50);
        return _node.SyncClock(p, nodeClockP);
    }
}