using Loopy.Data;

namespace Loopy.Interfaces;

internal interface INdcStore
{
    NdcObject Fetch(Key k);

    void StripCausality();

    NodeClock GetClock();

    (NodeClock clock, List<(Key, NdcObject)> missingObjects) SyncClock(NodeId peer, NodeClock peerClock);

    void SyncRepair(NodeId peer, NodeClock peerClock, List<(Key, NdcObject)> missingObjects);
}
