using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
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
    [ProtoMember(1)] public required ObjectMsg Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeUpdate, RpcDirection.Request)]
[ProtoContract]
public class NodeUpdateRequest : IMessage
{
    [ProtoMember(1)] public required string Key { get; set; }
    [ProtoMember(2)] public required ObjectMsg Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeUpdate, RpcDirection.Response)]
[ProtoContract]
public class NodeUpdateResponse : IMessage
{
    [ProtoMember(1)] public required ObjectMsg Obj { get; set; }
}

[RpcMessage(RpcOperation.NodeSyncClock, RpcDirection.Request)]
[ProtoContract]
public class NodeSyncClockRequest : IMessage
{
    [ProtoMember(1)] public required int NodeId { get; set; }
    [ProtoMember(2)] public required NodeClockMsg NodeClock { get; set; }
}

[RpcMessage(RpcOperation.NodeSyncClock, RpcDirection.Response)]
[ProtoContract]
public class NodeSyncClockResponse : IMessage
{
    [ProtoMember(1)] public required NodeClockMsg NodeClock { get; set; }
    [ProtoMember(2)] public required Dictionary<string, ObjectMsg> MissingObjects { get; set; }
}

[ProtoContract]
public class NodeClockMsg
{
    [ProtoMember(1)] public required List<(int node, int @base, int[] bitmap)> NodeClock { get; set; }

    public static implicit operator NodeClockMsg(Map<NodeId, UpdateIdSet> nc)
    {
        return new NodeClockMsg
        {
            NodeClock = nc.Select(kv => (kv.Key.Id, kv.Value.Base, kv.Value.Bitmap.ToArray())).ToList(),
        };
    }

    public static implicit operator Map<NodeId, UpdateIdSet>(NodeClockMsg nc)
    {
        return new Map<NodeId, UpdateIdSet>(nc.NodeClock.Select(kv => new KeyValuePair<NodeId, UpdateIdSet>(kv.node, new UpdateIdSet(kv.bitmap))));
    }
}

[ProtoContract]
public class ObjectMsg
{
    [ProtoMember(1)] public required List<(int node, int update, string value)> DotValues { get; set; }
    [ProtoMember(2)] public required CausalContextMsg CausalContext { get; set; }
    [ProtoMember(3)] public required List<(int prio, int update)> FifoBarriers { get; set; }

    public static implicit operator ObjectMsg(Data.Object obj)
    {
        return new ObjectMsg
        {
            DotValues = obj.DotValues.Select(kv => (kv.Key.NodeId.Id, kv.Key.UpdateId, kv.Value.Data)).ToList(),
            CausalContext = obj.CausalContext,
            FifoBarriers = obj.FifoBarriers.Select(kv => ((int)kv.Key, kv.Value)).ToList(),
        };
    }

    public static implicit operator Data.Object(ObjectMsg obj)
    {
        return new Data.Object
        {
            DotValues = new (obj.DotValues.Select(t => new KeyValuePair<Dot, Value>(new Dot(t.node, t.update), t.value))),
            CausalContext = obj.CausalContext,
            FifoBarriers = new (obj.FifoBarriers.Select(t => new KeyValuePair<Priority, int>((Priority)t.prio, t.update))),
        };
    }
}

[ProtoContract]
public class CausalContextMsg
{
    [ProtoMember(1)] public List<(int node, int update)> CausalContext { get; set; } = new();

    public static implicit operator CausalContextMsg(CausalContext cc)
    {
        return new CausalContextMsg { CausalContext = cc.Select(kv => (kv.Key.Id, kv.Value)).ToList() };
    }

    public static implicit operator CausalContext(CausalContextMsg cc)
    {
        return new CausalContext(cc.CausalContext.Select(t => new KeyValuePair<NodeId, int>(t.node, t.update)));
    }
}
