using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using Object = Loopy.Data.Object;

namespace Loopy;

public class LocalNodeApi : INodeApi
{
    private readonly Node _node;

    public LocalNodeApi(Node node) => _node = node;

    public NodeId NodeId => _node.Id;

    public async Task<IDisposable> Lock(CancellationToken cancellationToken = default)
    {
        return await _node.NodeLock.EnterAsync(cancellationToken);
    }

    public Task<Object> Fetch(Key k, ConsistencyMode mode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_node.Fetch(k, mode));
    }

    public Task<Object> Update(Key k, Object o, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_node.Update(k, o));
    }

    public void SendUpdate(Key k, Object o) => _node.Update(k, o);

    public Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncClock(
        NodeId peer, Map<NodeId, UpdateIdSet> nodeClockPeer, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_node.SyncClock(peer, nodeClockPeer));
    }

    public Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncFifoClock(
        NodeId peer, Priority prio, Map<NodeId, UpdateIdSet> nodeClockPeer, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_node.SyncFifoClock(peer, prio, nodeClockPeer));
    }
}
