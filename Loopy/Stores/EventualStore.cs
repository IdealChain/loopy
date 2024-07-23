using Loopy.Data;
using NLog;

namespace Loopy.Stores;

internal class EventualStore : NdcStoreBase
{
    private readonly Node _node;

    public EventualStore(Node node) : base(node.Id, node.Context)
    {
        _node = node;
    }

    public new NdcObject Update(Key k, NdcObject o)
    {
        var m = base.Update(k, o);
        _node.Logger.Trace("stored [{Stored}]", m);
        return m;
    }

    public Dot GetNextVersion()
    {
        var n = _node.Id;
        var c = NodeClock[n].Max + 1;
        return (n, c);
    }
}
