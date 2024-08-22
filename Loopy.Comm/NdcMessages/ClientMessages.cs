using Loopy.Core.Enums;
using ProtoBuf;
using System.Text.Json.Serialization;

namespace Loopy.Comm.NdcMessages;

[ProtoContract]
public class ClientGetRequest : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("q"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int? Quorum { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("cm"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ConsistencyMode? Mode { get; set; }
}

[ProtoContract]
public class ClientGetResponse : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("values")]
    public required string[]? Values { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("cc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<int, int>? CausalContext { get; set; }
}

[ProtoContract]
public class ClientPutRequest : NdcMessage
{
    [ProtoMember(1)]
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("value")]
    public required string? Value { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("cc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<int, int>? CausalContext { get; set; }
}

[ProtoContract]
public class ClientPutResponse : NdcMessage
{ }
