using ProtoBuf;
using System.Diagnostics;

namespace Loopy.Comm.Messages;

[ProtoContract]
public class MessageHeader
{
    [ProtoMember(1)] public RpcOperation Type { get; set; }
    [ProtoMember(2)] public RpcDirection Direction { get; set; }
}


[AttributeUsage(AttributeTargets.Class)]
public class RpcMessageAttribute : Attribute
{
    public RpcOperation Type { get; }
    public RpcDirection Direction { get; }

    public RpcMessageAttribute(RpcOperation type, RpcDirection direction)
    {
        Debug.Assert(type != RpcOperation.None);

        Type = type;
        Direction = direction;
    }
}

public enum RpcOperation : byte
{
    None,
    ClientGet,
    ClientPut,
    NodeFetch,
    NodeUpdate,
    NodeSyncClock,
}

public enum RpcDirection : byte
{
    Request,
    Response,
}
