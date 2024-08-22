using ProtoBuf;
using System.Text.Json.Serialization;

namespace Loopy.Comm.NdcMessages;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ClientGetRequest), "get")]
[JsonDerivedType(typeof(ClientGetResponse), "get_ok")]
[JsonDerivedType(typeof(ClientPutRequest), "put")]
[JsonDerivedType(typeof(ClientPutResponse), "put_ok")]
[JsonDerivedType(typeof(NodeFetchRequest), "fetch")]
[JsonDerivedType(typeof(NodeFetchResponse), "fetch_ok")]
[JsonDerivedType(typeof(NodeUpdateRequest), "update")]
[JsonDerivedType(typeof(NodeUpdateResponse), "update_ok")]
[JsonDerivedType(typeof(NodeSyncRequest), "sync")]
[JsonDerivedType(typeof(NodeSyncResponse), "sync_ok")]
[ProtoContract]
[ProtoInclude(01, typeof(ClientGetRequest))]
[ProtoInclude(02, typeof(ClientGetResponse))]
[ProtoInclude(03, typeof(ClientPutRequest))]
[ProtoInclude(04, typeof(ClientPutResponse))]
[ProtoInclude(05, typeof(NodeFetchRequest))]
[ProtoInclude(06, typeof(NodeFetchResponse))]
[ProtoInclude(07, typeof(NodeUpdateRequest))]
[ProtoInclude(08, typeof(NodeUpdateResponse))]
[ProtoInclude(09, typeof(NodeSyncRequest))]
[ProtoInclude(10, typeof(NodeSyncResponse))]
public abstract class NdcMessage { }
