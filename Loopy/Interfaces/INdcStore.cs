using Loopy.Data;

namespace Loopy.Interfaces;

internal interface INdcStore
{
    NdcObject Fetch(Key k);

    void StripCausality();

    ModeSyncRequest GetSyncRequest();

    ModeSyncResponse SyncClock(NodeId peer, ModeSyncRequest request);

    void SyncRepair(NodeId peer, ModeSyncResponse response);
}
