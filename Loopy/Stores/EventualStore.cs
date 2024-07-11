using Loopy.Data;
using Loopy.Interfaces;

namespace Loopy.Stores;

internal class EventualStore : NdcStoreBase, INdcStore
{
    public EventualStore(Node node) : base(node.Id, node.Context)
    { }

    public new NdcObject Update(Key k, NdcObject o)
    {
        return base.Update(k, o);
    }
}
