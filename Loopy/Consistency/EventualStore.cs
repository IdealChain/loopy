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
        // just fetch filled object from eventual store
        return _node.Fill(k, _node.Storage[k], _node.NodeClock);
    }
}
