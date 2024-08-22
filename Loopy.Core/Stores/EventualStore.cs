using Loopy.Core.Data;
using Loopy.Core.Interfaces;

namespace Loopy.Core.Stores;

internal class EventualStore : NdcStoreBase
{
    public EventualStore(INodeContext context) : base(context)
    {
    }

    public new NdcObject Update(Key k, NdcObject o)
    {
        var m = base.Update(k, o);
        Context.Logger.Debug("stored [{Stored}]", m);
        return m;
    }

    public Dot GetNextVersion()
    {
        var n = Context.NodeId;
        var c = NodeClock[n].Max + 1;
        return (n, c);
    }
}
