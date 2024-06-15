using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using Object = Loopy.Data.Object;

namespace Loopy.Consistency;

public class FifoStore : IConsistencyStore
{
    private readonly Dictionary<Priority, FifoPriorityStore> _priorityStores;

    public FifoStore(Node node)
    {
        _priorityStores = Enum.GetValues<Priority>().ToDictionary(
            p => p,
            p => new FifoPriorityStore(node, p));
    }

    public void CheckMerge(Key k, Object o)
    {
        // forward notification to all priority specific stores
        foreach (var priorityStore in _priorityStores.Values)
            priorityStore.CheckMerge(k, o);
    }

    public Object Fetch(Key k, Priority p = default) => _priorityStores[p].Fetch(k);

    public Map<NodeId, UpdateIdSet> GetClock(Priority prio) => _priorityStores[prio].GetClock();

    public (Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects) SyncClock(
        NodeId p, Priority prio, Map<NodeId, UpdateIdSet> pNodeClock)
    {
        return _priorityStores[prio].SyncClock(p, pNodeClock);
    }

    public void SyncRepair(NodeId p, Priority prio, Map<NodeId, UpdateIdSet> pNodeClock, List<(Key, Object)> missingObjects)
    {
        _priorityStores[prio].SyncRepair(p, pNodeClock, missingObjects);
    }
}
