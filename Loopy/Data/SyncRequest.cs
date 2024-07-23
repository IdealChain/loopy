using Loopy.Enums;

namespace Loopy.Data;

public class SyncRequest : Map<ConsistencyMode, ModeSyncRequest>
{
    public NodeId Peer { get; init; }
}

public class ModeSyncRequest
{
    public NodeClock PeerClock { get; init; }
}

public class SyncResponse : Map<ConsistencyMode, ModeSyncResponse>
{
    public NodeId Peer { get; init; }
}

public class ModeSyncResponse
{
    public NodeClock PeerClock { get; set; } = new();

    public List<(Key, NdcObject)> MissingObjects { get; set; } = new();

    public List<(NodeId node, UpdateIdRange range, List<(Key, NdcObject)> objects)> BufferedSegments { get; set; } = new();
}
