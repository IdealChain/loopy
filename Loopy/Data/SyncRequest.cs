using Loopy.Enums;

namespace Loopy.Data;

public class SyncRequest : Map<ConsistencyMode, NodeClock>
{
    public NodeId Peer { get; set; }
}

public class SyncResponse : Map<ConsistencyMode, (NodeClock clock, List<(Key, NdcObject)> missingObjects)>
{
    public NodeId Peer { get; set; }
}
