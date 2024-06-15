using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using Object = Loopy.Data.Object;

namespace Loopy.Consistency;

public class EventualStore : IConsistencyStore
{
    private readonly Node _node;

    public EventualStore(Node node) => _node = node;

    public void CheckMerge(Key k, Object o)
    {
        // nothing to do
    }

    public Object Fetch(Key k, Priority p = default)
    {
        if (!_node.Storage.TryGetValue(k, out var obj) || obj.IsEmpty)
            obj = new Object();
        
        // just fetch filled object from eventual store
        return _node.Fill(k, obj, _node.NodeClock);
    }
}
