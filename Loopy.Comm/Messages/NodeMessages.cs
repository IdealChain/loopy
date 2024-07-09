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
    [ProtoMember(1)] public required ObjectMsg? Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeUpdate, RpcDirection.Request)]
[ProtoContract]
public class NodeUpdateRequest : IMessage
{
    [ProtoMember(1)] public required string Key { get; set; }
    [ProtoMember(2)] public required ObjectMsg? Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeUpdate, RpcDirection.Response)]
[ProtoContract]
public class NodeUpdateResponse : IMessage
{
    [ProtoMember(1)] public required ObjectMsg? Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeSyncClock, RpcDirection.Request)]
[ProtoContract]
public class NodeSyncClockRequest : IMessage
{
    [ProtoMember(1)] public required int NodeId { get; set; }
    [ProtoMember(2)] public required NodeClockMsg? NodeClock { get; set; }
}

[RpcMessage(RpcOperation.NodeSyncClock, RpcDirection.Response)]
[ProtoContract]
public class NodeSyncClockResponse : IMessage
{
    [ProtoMember(1)] public required NodeClockMsg? NodeClock { get; set; }
    [ProtoMember(2)] public required Dictionary<string, ObjectMsg>? MissingObjects { get; set; }
}

[ProtoContract]
public class NodeClockMsg
{
    [ProtoMember(1)] public required List<(int node, int @base, int[] bitmap)>? NodeClock { get; set; }

    public static implicit operator NodeClockMsg(Map<NodeId, UpdateIdSet> nc)
    {
        return new NodeClockMsg
        {
            NodeClock = nc.Select(kv => (kv.Key.Id, kv.Value.Base, kv.Value.Bitmap.ToArray())).ToList(),
        };
    }

    public static implicit operator Map<NodeId, UpdateIdSet>(NodeClockMsg msg)
    {
        var nc = new Map<NodeId, UpdateIdSet>();

        if (msg.NodeClock != null)
            nc.MergeIn(msg.NodeClock.Select(kv => ((NodeId)kv.node, new UpdateIdSet(kv.@base, kv.bitmap ?? Enumerable.Empty<int>()))));

        return nc;
    }
}

[ProtoContract]
public class ObjectMsg
{
    [ProtoMember(1)] public required List<(int node, int update, string value)>? DotValues { get; set; }
    [ProtoMember(2)] public required List<(int node, int update, FifoDistancesMsg fd)>? FifoDistances { get; set; }
    [ProtoMember(3)] public required CausalContextMsg? CausalContext { get; set; }

    public static implicit operator ObjectMsg(Data.Object obj)
    {
        return new ObjectMsg
        {
            DotValues = obj.DotValues.Select(kv =>
                (kv.Key.NodeId.Id, kv.Key.UpdateId, kv.Value.Data)).ToList(),
            FifoDistances = obj.FifoDistances.Select(kv =>
                (kv.Key.NodeId.Id, kv.Key.UpdateId, (FifoDistancesMsg)kv.Value)).ToList(),
            CausalContext = obj.CausalContext,
        };
    }

    public static implicit operator Data.Object(ObjectMsg msg)
    {
        var obj = new Data.Object();

        if (msg.DotValues != null)
            obj.DotValues.MergeIn(msg.DotValues.Select(t => (new Dot(t.node, t.update), (Value)t.value)));

        if (msg.FifoDistances != null)
            obj.FifoDistances.MergeIn(msg.FifoDistances.Select(t => (new Dot(t.node, t.update), (FifoDistances)t.fd)));

        if (msg.CausalContext != null)
            obj.CausalContext.MergeIn((CausalContext)msg.CausalContext);

        return obj;
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

    public static implicit operator CausalContext(CausalContextMsg msg)
    {
        var cc = new CausalContext();

        if (msg.CausalContext != null)
            cc.MergeIn(msg.CausalContext.Select(t => ((NodeId)t.node, t.update)));

        return cc;
    }
}

[ProtoContract]
public class FifoDistancesMsg
{
    [ProtoMember(1)] public List<(Priority prio, int dist)>? FifoDistances { get; set; }

    public static implicit operator FifoDistancesMsg(FifoDistances fd)
    {
        return new FifoDistancesMsg { FifoDistances = fd.Select(kv => (kv.Key, kv.Value)).ToList() };
    }

    public static implicit operator FifoDistances(FifoDistancesMsg msg)
    {
        var fd = new FifoDistances();

        if (msg.FifoDistances != null)
            fd.MergeIn(msg.FifoDistances.Select(t => (t.prio, t.dist)));

        return fd;
    }
}
