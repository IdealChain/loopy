using Loopy.Core.Data;
using Loopy.Core.Enums;
using ProtoBuf;
using System.Text.Json.Serialization;

namespace Loopy.Comm.NdcMessages;

[ProtoContract]
public class NodeFetchRequest : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [ProtoMember(2), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("cm")]
    public ConsistencyMode Mode { get; set; }
}

[ProtoContract]
public class NodeFetchResponse : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("obj")]
    public required NdcObjectMsg? Obj { get; set; }
}

[ProtoContract]
public class NodeUpdateRequest : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("obj")]
    public required NdcObjectMsg? Obj { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("ack")]
    public bool SendResponse { get; set; } = true;
}

[ProtoContract]
public class NodeUpdateResponse : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("obj")]
    public required NdcObjectMsg? Obj { get; set; }
}

[ProtoContract]
public class NodeSyncRequest : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("node")]
    public required int NodeId { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("stores")]
    public required Dictionary<ConsistencyMode, NodeSyncRequestStoreMsg>? Stores { get; set; }

    public static implicit operator NodeSyncRequest(SyncRequest req)
    {
        return new NodeSyncRequest
        {
            NodeId = req.Peer.Id,
            Stores = req.ToDictionary(kv => kv.Key, kv => (NodeSyncRequestStoreMsg)kv.Value),
        };
    }

    public static implicit operator SyncRequest(NodeSyncRequest msg)
    {
        var req = new SyncRequest { Peer = msg.NodeId };

        if (msg.Stores != null)
            req.MergeIn(msg.Stores.Select(kv => (kv.Key, (ModeSyncRequest)kv.Value)));

        return req;
    }
}

[ProtoContract]
public class NodeSyncRequestStoreMsg
{
    [ProtoMember(1)]
    [JsonPropertyName("clock")]
    public required List<UpdateIdSetMsg>? NodeClock { get; set; }

    public static explicit operator NodeSyncRequestStoreMsg(ModeSyncRequest req)
    {
        return new NodeSyncRequestStoreMsg
        {
            NodeClock = req.PeerClock.Select(kv => (UpdateIdSetMsg)kv).ToList(),
        };
    }

    public static explicit operator ModeSyncRequest(NodeSyncRequestStoreMsg? msg)
    {
        var req = new ModeSyncRequest();

        if (msg?.NodeClock != null)
            req.PeerClock.MergeIn(msg.NodeClock.Select(t => (KeyValuePair<NodeId, UpdateIdSet>)t));

        return req;
    }
}

[ProtoContract]
public class NodeSyncResponse : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("node")]
    public required int NodeId { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("stores")]
    public required Dictionary<ConsistencyMode, NodeSyncStoreResponseMsg>? Stores { get; set; }

    public static implicit operator NodeSyncResponse(SyncResponse rep)
    {
        return new NodeSyncResponse
        {
            NodeId = rep.Peer.Id,
            Stores = rep.ToDictionary(kv => kv.Key, kv => (NodeSyncStoreResponseMsg)kv.Value),
        };
    }

    public static implicit operator SyncResponse(NodeSyncResponse msg)
    {
        var req = new SyncResponse { Peer = msg.NodeId };

        if (msg?.Stores != null)
            req.MergeIn(msg.Stores.Select(kv => (kv.Key, (ModeSyncResponse)kv.Value)));

        return req;
    }
}

[ProtoContract]
public class NodeSyncStoreResponseMsg
{
    [ProtoMember(1)]
    [JsonPropertyName("clock")]
    public required List<UpdateIdSetMsg>? NodeClock { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("objs")]
    public required Dictionary<string, NdcObjectMsg>? MissingObjects { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("bs")]
    public required List<BufferedSegmentMsg>? BufferedSegments { get; set; }

    public static implicit operator NodeSyncStoreResponseMsg(ModeSyncResponse rep)
    {
        return new NodeSyncStoreResponseMsg
        {
            NodeClock = rep.PeerClock.Select(kv => (UpdateIdSetMsg)kv).ToList(),
            MissingObjects = rep.MissingObjects.ToDictionary(kv => kv.Item1.Name, kv => (NdcObjectMsg)kv.Item2),
            BufferedSegments = rep.BufferedSegments.Select(bs => new BufferedSegmentMsg
            {
                Node = bs.node.Id,
                First = bs.range.First,
                Last = bs.range.Last,
                Objects = bs.objects.ToDictionary(kv => kv.Item1.Name, kv => (NdcObjectMsg)kv.Item2),
            }).ToList(),
        };
    }

    public static implicit operator ModeSyncResponse(NodeSyncStoreResponseMsg? msg)
    {
        var rep = new ModeSyncResponse();

        if (msg?.NodeClock != null)
            rep.PeerClock.MergeIn(msg.NodeClock.Select(t => (KeyValuePair<NodeId, UpdateIdSet>)t));

        if (msg?.MissingObjects != null)
        {
            rep.MissingObjects = msg.MissingObjects
                .Where(kv => !string.IsNullOrEmpty(kv.Key))
                .Select(kv => ((Key)kv.Key, (NdcObject)kv.Value)).ToList();
        }

        if (msg?.BufferedSegments != null)
        {
            rep.BufferedSegments = msg.BufferedSegments.Select(bs => (
                (NodeId)bs.Node,
                new UpdateIdRange(bs.First, bs.Last),
                bs.Objects
                    ?.Where(kv => !string.IsNullOrEmpty(kv.Key))
                    ?.Select(kv => ((Key)kv.Key, (NdcObject)kv.Value))?.ToList() ?? []
            )).ToList();
        }

        return rep;
    }
}

[ProtoContract]
public class BufferedSegmentMsg
{
    [ProtoMember(1)]
    [JsonPropertyName("node")]
    public int Node { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("fst")]
    public int First { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("lst")]
    public int Last { get; set; }

    [ProtoMember(4)]
    [JsonPropertyName("objs")]
    public Dictionary<string, NdcObjectMsg>? Objects { get; set; }
}

[ProtoContract]
public class UpdateIdSetMsg
{
    [ProtoMember(1)]
    [JsonPropertyName("node")]
    public int Node { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("base"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Base { get; set; }

    [ProtoMember(3, IsPacked = true)]
    [JsonPropertyName("bitmap"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int[]? Bitmap { get; set; }

    public static implicit operator UpdateIdSetMsg(KeyValuePair<NodeId, UpdateIdSet> kv)
    {
        return new UpdateIdSetMsg
        {
            Node = kv.Key.Id,
            Base = kv.Value.Base,
            Bitmap = kv.Value.Bitmap.ToArray(),
        };
    }

    public static implicit operator KeyValuePair<NodeId, UpdateIdSet>(UpdateIdSetMsg msg)
    {
        var set = new UpdateIdSet(msg.Base, msg.Bitmap ?? Enumerable.Empty<int>());
        return new KeyValuePair<NodeId, UpdateIdSet>(msg.Node, set);
    }
}

[ProtoContract]
public class NdcObjectMsg
{
    [ProtoMember(1)]
    [JsonPropertyName("dvs")]
    public required List<DotValueMsg>? DotValues { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("cc")]
    public Dictionary<int, int>? CausalContext { get; set; }

    public static implicit operator NdcObjectMsg(NdcObject obj)
    {
        return new NdcObjectMsg
        {
            DotValues = obj.DotValues.Select(dv => (DotValueMsg)dv).ToList(),
            CausalContext = obj.CausalContext,
        };
    }

    public static implicit operator NdcObject(NdcObjectMsg? msg)
    {
        var obj = new NdcObject();

        if (msg?.DotValues != null)
            obj.DotValues.MergeIn(msg.DotValues.Select(t => (KeyValuePair<Dot, (Value value, int[] fifoDistances)>)t));

        if (msg?.CausalContext != null)
            obj.CausalContext.MergeIn((CausalContext)msg.CausalContext);

        return obj;
    }
}

[ProtoContract]
public class DotValueMsg
{
    [ProtoMember(1)]
    [JsonPropertyName("n")]
    public int Node { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("u")]
    public int UpdateId { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("v")]
    public string? Value { get; set; }

    [ProtoMember(4, IsPacked = true)]
    [JsonPropertyName("fd"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int[]? FifoDistances { get; set; }

    public static implicit operator DotValueMsg(KeyValuePair<Dot, (Value value, int[] fifoDistances)> kv)
    {
        return new DotValueMsg
        {
            Node = kv.Key.NodeId.Id,
            UpdateId = kv.Key.UpdateId,
            Value = kv.Value.value.Data,
            FifoDistances = kv.Value.fifoDistances,
        };
    }

    public static implicit operator KeyValuePair<Dot, (Value value, int[] fifoDistances)>(DotValueMsg msg)
    {
        var dot = new Dot(msg.Node, msg.UpdateId);
        var value = ((Value)msg.Value, msg.FifoDistances ?? []);
        return new KeyValuePair<Dot, (Value value, int[] fifoDistances)>(dot, value);
    }
}
