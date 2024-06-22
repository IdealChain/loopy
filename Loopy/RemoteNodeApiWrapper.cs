using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using Object = Loopy.Data.Object;

namespace Loopy;

public class RemoteNodeApiWrapper : IRemoteNodeApi
{
    private readonly Node _node;

    public RemoteNodeApiWrapper(Node node)
    {
        _node = node;
    }

    public async Task<Object> Fetch(Key k, ConsistencyMode mode)
    {
        using (await _node.NodeLock.Enter(CancellationToken.None))
            return _node.Fetch(k, mode);
    }

    public async Task<Object> Update(Key k, Object o)
    {
        using (await _node.NodeLock.Enter(CancellationToken.None))
            return _node.Update(k, o);
    }

    public async Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncClock(
        NodeId p, Map<NodeId, UpdateIdSet> nodeClockP)
    {
        using (await _node.NodeLock.Enter(CancellationToken.None))
            return _node.SyncClock(p, nodeClockP);
    }

    public async Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncFifoClock(
        NodeId p, Priority prio, Map<NodeId, UpdateIdSet> nodeClockP)
    {
        using (await _node.NodeLock.Enter(CancellationToken.None))
            return _node.SyncFifoClock(p, prio, nodeClockP);
    }
}
