using Loopy.Data;
using Loopy.Enums;
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

[RpcMessage(RpcOperation.NodeSyncClock, RpcDirection.Request)]
[ProtoContract]
public class NodeSyncClockRequest : IMessage
{
    [ProtoMember(1)] public required int NodeId { get; set; }
    [ProtoMember(2)] public required List<(ConsistencyMode mode, NodeClockMsg clock)>? NodeClocks { get; set; }

    public static implicit operator NodeSyncClockRequest(SyncRequest req)
    {
        return new NodeSyncClockRequest
        {
            NodeId = req.Peer.Id,
            NodeClocks = req.Select(kv => (kv.Key, (NodeClockMsg)kv.Value)).ToList()
        };
    }

    public static implicit operator SyncRequest(NodeSyncClockRequest msg)
    {
        var req = new SyncRequest { Peer = msg.NodeId };

        if (msg.NodeClocks != null)
            req.MergeIn(msg.NodeClocks.Select(t => (t.mode, (NodeClock)t.clock)));

        return req;
    }
}

[RpcMessage(RpcOperation.NodeSyncClock, RpcDirection.Response)]
[ProtoContract]
public class NodeSyncClockResponse : IMessage
{
    [ProtoMember(1)] public required int NodeId { get; set; }
    [ProtoMember(2)] public required List<(ConsistencyMode mode, NodeClockMsg clock, NdcObjectsMsg missingObjects)>? NodeResponse { get; set; }

    public static implicit operator NodeSyncClockResponse(SyncResponse rep)
    {
        return new NodeSyncClockResponse
        {
            NodeId = rep.Peer.Id,
            NodeResponse = rep.Select(kv => (kv.Key, (NodeClockMsg)kv.Value.clock, (NdcObjectsMsg)kv.Value.missingObjects)).ToList()
        };
    }

    public static implicit operator SyncResponse(NodeSyncClockResponse msg)
    {
        var req = new SyncResponse { Peer = msg.NodeId };

        if (msg.NodeResponse != null)
            req.MergeIn(msg.NodeResponse.Select(t => (t.mode, ((NodeClock)t.clock, (List<(Key, NdcObject)>)t.missingObjects))));

        return req;
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
            (kv.Key.NodeId.Id, kv.Key.UpdateId, kv.Value.value.Data, kv.Value.fifoDistances)).ToList(),
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
            objs.AddRange(msg.Objects.Select(t => ((Key)t.key, (NdcObject)t.obj)));

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
