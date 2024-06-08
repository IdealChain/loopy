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

    public Object Fetch(Key k, Priority p = default)
    {
        if (!_priorityStores.TryGetValue(p, out var priorityStore))
            return new Object();

        return priorityStore.Fetch(k);
    }
}
