using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using ProtoBuf;

namespace Loopy.Comm.Messages;

[RpcMessage(RpcOperation.ClientGet, RpcDirection.Request)]
[ProtoContract]
public class ClientGetRequest : IMessage
{
    [ProtoMember(1)] public required string Key { get; set; }
    [ProtoMember(2)] public int? Quorum { get; set; }
    [ProtoMember(3)] public ConsistencyMode? Mode { get; set; }
}

[RpcMessage(RpcOperation.ClientGet, RpcDirection.Response)]
[ProtoContract]
public class ClientGetResponse : IMessage
{
    [ProtoMember(1)] public required string[] Values { get; set; }
    [ProtoMember(2)] public required CausalContextMsg CausalContext { get; set; }
}

[RpcMessage(RpcOperation.ClientPut, RpcDirection.Request)]
[ProtoContract]
public class ClientPutRequest : IMessage
{
    [ProtoMember(1)] public required string Key { get; set; }
    [ProtoMember(2)] public required string Value { get; set; }
    [ProtoMember(3)] public CausalContextMsg? CausalContext { get; set; }
    [ProtoMember(4)] public ReplicationMode? Mode { get; set; }
}

[RpcMessage(RpcOperation.ClientPut, RpcDirection.Response)]
[ProtoContract]
public class ClientPutResponse : IMessage
{
    [ProtoMember(1)] public bool Success { get; set; }
}
