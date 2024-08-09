using Loopy.Core.Data;
using Loopy.Core.Enums;
using ProtoBuf;

namespace Loopy.Comm.Messages;

[RpcMessage(RpcOperation.NodeFetch, RpcDirection.Request)]
[ProtoContract]
public class NodeFetchRequest : IMessage
{
    [ProtoMember(1)] public required string Key { get; set; }
    [ProtoMember(2)] public ConsistencyMode Mode { get; set; }
}

[RpcMessage(RpcOperation.NodeFetch, RpcDirection.Response)]
[ProtoContract]
public class NodeFetchResponse : IMessage
{
    [ProtoMember(1)] public required NdcObjectMsg? Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeUpdate, RpcDirection.Request)]
[ProtoContract]
public class NodeUpdateRequest : IMessage
{
    [ProtoMember(1)] public required string Key { get; set; }
    [ProtoMember(2)] public required NdcObjectMsg? Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeUpdate, RpcDirection.Response)]
[ProtoContract]
public class NodeUpdateResponse : IMessage
{
    [ProtoMember(1)] public required NdcObjectMsg? Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeSync, RpcDirection.Request)]
[ProtoContract]
public class NodeSyncRequest : IMessage
{
    [ProtoMember(1)] public required int NodeId { get; set; }
    [ProtoMember(2)] public required List<(ConsistencyMode mode, NodeSyncRequestStoreMsg req)>? Requests { get; set; }

    public static implicit operator NodeSyncRequest(SyncRequest req)
    {
        return new NodeSyncRequest
        {
            NodeId = req.Peer.Id,
            Requests = req.Select(kv => (kv.Key, (NodeSyncRequestStoreMsg)kv.Value)).ToList(),
        };
    }

    public static implicit operator SyncRequest(NodeSyncRequest msg)
    {
        var req = new SyncRequest { Peer = msg.NodeId };

        if (msg.Requests != null)
            req.MergeIn(msg.Requests.Select(t => (t.mode, (ModeSyncRequest)t.req)));

        return req;
    }
}

[ProtoContract]
public class NodeSyncRequestStoreMsg
{
    [ProtoMember(1)] public required NodeClockMsg? Clock { get; set; }

    public static explicit operator NodeSyncRequestStoreMsg(ModeSyncRequest req)
    {
        return new NodeSyncRequestStoreMsg { Clock = req.PeerClock, };
    }

    public static explicit operator ModeSyncRequest(NodeSyncRequestStoreMsg? msg)
    {
        return new ModeSyncRequest { PeerClock = msg?.Clock != null ? (NodeClock)msg.Clock : new() };
    }
}

[RpcMessage(RpcOperation.NodeSync, RpcDirection.Response)]
[ProtoContract]
public class NodeSyncResponse : IMessage
{
    [ProtoMember(1)] public required int NodeId { get; set; }
    [ProtoMember(2)] public required List<(ConsistencyMode mode, NodeSyncStoreResponseMsg response)>? NodeResponse { get; set; }

    public static implicit operator NodeSyncResponse(SyncResponse rep)
    {
        return new NodeSyncResponse
        {
            NodeId = rep.Peer.Id,
            NodeResponse = rep.Select(kv => (kv.Key, (NodeSyncStoreResponseMsg)kv.Value)).ToList()
        };
    }

    public static implicit operator SyncResponse(NodeSyncResponse? msg)
    {
        var req = new SyncResponse { Peer = msg.NodeId };

        if (msg?.NodeResponse != null)
            req.MergeIn(msg.NodeResponse.Select(t => (t.mode, (ModeSyncResponse)t.response)));

        return req;
    }
}

[ProtoContract]
public class NodeSyncStoreResponseMsg
{
    [ProtoMember(1)] public required NodeClockMsg? Clock { get; set; }

    [ProtoMember(2)] public required NdcObjectsMsg? MissingObjects { get; set; }

    [ProtoMember(3)] public required BufferedSegmentsMsg? BufferedSegments { get; set; }

    public static implicit operator NodeSyncStoreResponseMsg(ModeSyncResponse rep)
    {
        return new NodeSyncStoreResponseMsg
        {
            Clock = rep.PeerClock,
            MissingObjects = rep.MissingObjects,
            BufferedSegments = rep.BufferedSegments,
        };
    }

    public static implicit operator ModeSyncResponse(NodeSyncStoreResponseMsg? msg)
    {
        var rep = new ModeSyncResponse();

        if (msg?.Clock != null)
            rep.PeerClock = msg.Clock;

        if (msg?.MissingObjects != null)
            rep.MissingObjects = msg.MissingObjects;

        if (msg?.BufferedSegments != null)
            rep.BufferedSegments = msg.BufferedSegments;

        return rep;
    }
}

[ProtoContract]
public class BufferedSegmentsMsg
{
    [ProtoMember(1)] public required List<(int node, int first, int last, NdcObjectsMsg objects)>? Segments { get; set; }

    public static implicit operator BufferedSegmentsMsg(List<(NodeId node, UpdateIdRange range, List<(Key, NdcObject)> segment)> bufferedSegments)
    {
        return new BufferedSegmentsMsg
        {
            Segments = bufferedSegments.Select(t => (t.node.Id, t.range.First, t.range.Last, (NdcObjectsMsg)t.segment)).ToList(),
        };
    }

    public static implicit operator List<(NodeId node, UpdateIdRange range, List<(Key, NdcObject)> segment)>(BufferedSegmentsMsg? msg)
    {
        var bs = new List<(NodeId node, UpdateIdRange range, List<(Key, NdcObject)> segment)>();

        if (msg?.Segments != null)
            bs.AddRange(msg.Segments.Select(s => ((NodeId)s.node, new UpdateIdRange(s.first, s.last), (List<(Key, NdcObject)>)s.objects)));

        return bs;
    }
}

[ProtoContract]
public class NodeClockMsg
{
    [ProtoMember(1)] public required List<(int node, int @base, int[] bitmap)>? NodeClock { get; set; }

    public static implicit operator NodeClockMsg(NodeClock nc)
    {
        return new NodeClockMsg
        {
            NodeClock = nc.Select(kv => (kv.Key.Id, kv.Value.Base, kv.Value.Bitmap.ToArray())).ToList(),
        };
    }

    public static implicit operator NodeClock(NodeClockMsg? msg)
    {
        var nc = new NodeClock();

        if (msg?.NodeClock != null)
            nc.MergeIn(msg.NodeClock.Select(t => ((NodeId)t.node, new UpdateIdSet(t.@base, t.bitmap ?? Enumerable.Empty<int>()))));

        return nc;
    }
}

[ProtoContract]
public class NdcObjectMsg
{
    [ProtoMember(1)] public required List<(int node, int update, string? value, int[]? fds)>? DotValues { get; set; }
    [ProtoMember(2)] public required CausalContextMsg? CausalContext { get; set; }

    public static implicit operator NdcObjectMsg(NdcObject obj)
    {
        return new NdcObjectMsg
        {
            DotValues = obj.DotValues.Select(kv =>
                (kv.Key.NodeId.Id, kv.Key.UpdateId, kv.Value.value.Data, kv.Value.fifoDistances)).ToList()!,
            CausalContext = obj.CausalContext,
        };
    }

    public static implicit operator NdcObject(NdcObjectMsg? msg)
    {
        var obj = new NdcObject();

        if (msg?.DotValues != null)
            obj.DotValues.MergeIn(msg.DotValues.Select(t => (new Dot(t.node, t.update), ((Value)t.value, t.fds ?? []))));

        if (msg?.CausalContext != null)
            obj.CausalContext.MergeIn((CausalContext)msg.CausalContext);

        return obj;
    }
}

[ProtoContract]
public class NdcObjectsMsg()
{
    [ProtoMember(1)] public required List<(string key, NdcObjectMsg obj)>? Objects { get; set; }

    public static implicit operator NdcObjectsMsg(List<(Key key, NdcObject obj)> objs)
    {
        return new NdcObjectsMsg
        {
            Objects = objs.Select(kv => (kv.key.Name, (NdcObjectMsg)kv.obj)).ToList(),
        };
    }

    public static implicit operator List<(Key, NdcObject)>(NdcObjectsMsg? msg)
    {
        var objs = new List<(Key, NdcObject)>();

        if (msg?.Objects != null)
            objs.AddRange(msg.Objects
                .Where(t => !string.IsNullOrEmpty(t.key))
                .Select(t => ((Key)t.key, (NdcObject)t.obj)));

        return objs;
    }
}

[ProtoContract]
public class CausalContextMsg
{
    [ProtoMember(1)] public List<(int node, int update)>? CausalContext { get; set; }

    public static implicit operator CausalContextMsg(CausalContext cc)
    {
        return new CausalContextMsg { CausalContext = cc.Select(kv => (kv.Key.Id, kv.Value)).ToList() };
    }

    public static implicit operator CausalContext(CausalContextMsg? msg)
    {
        var cc = new CausalContext();

        if (msg?.CausalContext != null)
            cc.MergeIn(msg.CausalContext.Select(t => ((NodeId)t.node, t.update)));

        return cc;
    }
}
