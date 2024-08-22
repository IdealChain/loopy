#pragma warning disable IDE1006
// ReSharper disable InconsistentNaming

using Loopy.Comm.Extensions;
using Loopy.Comm.NdcMessages;
using Loopy.Core.Data;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Loopy.Comm.MaelstromMessages;

public class Envelope
{
    [JsonRequired] public required string src { get; init; }
    [JsonRequired] public required string dest { get; init; }
    [JsonRequired] public required MessageBase body { get; init; }

    public Envelope CreateReply(ResponseBase responseBody) => new()
    {
        src = dest,
        dest = src,
        body = responseBody.InReplyTo(body)
    };

    public override string ToString() => $"{src}=>{dest}: {body}";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ErrorResponse), "error")]
[JsonDerivedType(typeof(InitRequest), "init")]
[JsonDerivedType(typeof(InitOkResponse), "init_ok")]
[JsonDerivedType(typeof(ReadRequest), "read")]
[JsonDerivedType(typeof(ReadOkResponse), "read_ok")]
[JsonDerivedType(typeof(WriteRequest), "write")]
[JsonDerivedType(typeof(WriteOkResponse), "write_ok")]
[JsonDerivedType(typeof(CasRequest), "cas")]
[JsonDerivedType(typeof(CasOkResponse), "cas_ok")]
[JsonDerivedType(typeof(WrappedNdcRequest), "ndc")]
[JsonDerivedType(typeof(WrappedNdcResponse), "ndc_ok")]
public abstract class MessageBase
{
    [JsonRequired] public int msg_id { get; set; }

    private static int _lastId = 0;
    public static int GetUniqueId() => Interlocked.Increment(ref _lastId);

    public override string ToString() => $"[{msg_id}]";
}

public abstract class RequestBase : MessageBase
{
}

public abstract class ResponseBase : MessageBase
{
    [JsonRequired] public int in_reply_to { get; set; }

    public ResponseBase InReplyTo(MessageBase request)
    {
        Debug.Assert(request.msg_id > 0);
        in_reply_to = request.msg_id;
        return this;
    }

    public override string ToString() => $"[{msg_id}=>{in_reply_to}]";
}

public class ErrorResponse(ErrorCode code, string? text = null) : ResponseBase
{
    [JsonRequired] public ErrorCode code { get; init; } = code;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public string? text { get; init; } = text;

    public override string ToString() => $"{base.ToString()}: error {code} {text}";
}

public class InitRequest(string node_id, string[] node_ids) : RequestBase
{
    [JsonRequired] public string node_id { get; init; } = node_id;
    [JsonRequired] public string[] node_ids { get; init; } = node_ids;

    public override string ToString() => $"{base.ToString()}: init {node_id} {node_ids.AsCsv()}";
}

public class InitOkResponse : ResponseBase
{
    public override string ToString() => $"{base.ToString()}: init ok";
}

public class ReadRequest(string key) : RequestBase()
{
    [JsonRequired, JsonConverter(typeof(IntegerAutoConverter))] 
    public string key { get; init; } = key;

    public override string ToString() => $"{base.ToString()}: read {key}";
}

public class ReadOkResponse(string value) : ResponseBase
{
    [JsonRequired, JsonConverter(typeof(IntegerAutoConverter))] 
    public string value { get; init; } = value;

    public override string ToString() => $"{base.ToString()}: read ok {value}";
}

public class WriteRequest(string key, string value) : RequestBase
{
    [JsonRequired, JsonConverter(typeof(IntegerAutoConverter))]
    public string key { get; init; } = key;

    [JsonRequired, JsonConverter(typeof(IntegerAutoConverter))]
    public string value { get; init; } = value;

    public override string ToString() => $"{base.ToString()}: write {key}:={value}";
}

public class WriteOkResponse : ResponseBase
{
    public override string ToString() => $"{base.ToString()}: write ok";
}

public class CasRequest(string key, string from, string to) : RequestBase
{
    [JsonRequired, JsonConverter(typeof(IntegerAutoConverter))]
    public string key { get; init; } = key;

    [JsonRequired, JsonConverter(typeof(IntegerAutoConverter))]
    public string from { get; init; } = from;

    [JsonRequired, JsonConverter(typeof(IntegerAutoConverter))]
    public string to { get; init; } = to;

    public override string ToString() => $"{base.ToString()}: cas {key}: {from}=>{to}";
}

public class CasOkResponse : ResponseBase
{
    public override string ToString() => $"{base.ToString()}: cas ok";
}

public class WrappedNdcRequest(NdcMessage msg) : RequestBase
{
    [JsonRequired] public NdcMessage msg { get; init; } = msg;

    public override string ToString() => $"{base.ToString()}: ndc {msg.ToString() ?? "-"}";
}

public class WrappedNdcResponse(NdcMessage msg) : ResponseBase
{
    [JsonRequired] public NdcMessage msg { get; init; } = msg;

    public override string ToString() => $"{base.ToString()}: ndc ok {msg.ToString() ?? "-"}";
}
