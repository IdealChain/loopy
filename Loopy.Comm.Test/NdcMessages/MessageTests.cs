using Loopy.Comm.Extensions;
using Loopy.Comm.NdcMessages;
using Loopy.Comm.Sockets;
using Loopy.Core.Enums;
using NUnit.Framework;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Loopy.Comm.Test.NdcMessages;

public class MessageTests
{
    [Test]
    public void TestProtoDefinitions()
    {
        var options = new SchemaGenerationOptions();
        options.Types.Add(typeof(NdcMessage));
        var def = Serializer.GetProto(options);
        TestContext.Out.WriteLine(def);
        Assert.That(def, Does.Contain("ClientGetRequest"));
    }

    [Test]
    [TestCaseSource(nameof(MessageSource))]
    public void TestNetMQProtobufRoundtrip(NdcMessage msg)
    {
        var mq = NetMQProtobufSocket<NdcMessage>.Serialize((msg, null));
        TestContext.Out.WriteLine("{0} Bytes", mq.Sum(x => x.MessageSize));
        TestContext.Out.WriteLine(mq);

        var (msg2, header) = NetMQProtobufSocket<NdcMessage>.Deserialize(mq);
        Assert.That(msg2.GetType(), Is.EqualTo(msg.GetType()));
        Assert.That(header, Is.Null);
    }

    [Test]
    [TestCaseSource(nameof(MessageSource))]
    public void TestJsonRoundtrip(NdcMessage msg)
    {
        var line = JsonSocket<NdcMessage>.Serialize(msg);
        TestContext.Out.WriteLine($"{line.Length} Chars");
        TestContext.Out.WriteIndentedJson(line);

        var msg2 = JsonSocket<NdcMessage>.Deserialize(line);
        Assert.That(msg2?.GetType(), Is.EqualTo(msg.GetType()));
    }

    private static IEnumerable<NdcMessage> MessageSource
    {
        get
        {
            var obj = new NdcObjectMsg
            {
                DotValues = [new() { Node = 3, UpdateId = 7, Value = "value", FifoDistances = [1, 2, 3, 4] }],
                CausalContext = new() { { 3, 7 } },
            };

            var requestMsg = new NodeSyncRequestStoreMsg
            {
                NodeClock = [new() { Node = 3, Base = 3, Bitmap = [5] }]
            };
            var responseMsg = new NodeSyncStoreResponseMsg
            {
                NodeClock = [new() { Node = 3, Base = 3, Bitmap = [5] }],
                MissingObjects = new() { { "key", obj } },
                BufferedSegments =
                [
                    new()
                    {
                        Node = 3,
                        First = 7,
                        Last = 10,
                        Objects = new() { { "key", obj } }
                    }
                ]
            };

            yield return new ClientGetRequest { Key = "key" };
            yield return new ClientGetResponse { Values = ["value"], CausalContext = obj.CausalContext };

            yield return new ClientPutRequest { Key = "key", Value = "value" };
            yield return new ClientPutResponse();

            yield return new NodeFetchRequest { Key = "key", Mode = ConsistencyMode.Fifo };
            yield return new NodeFetchResponse { Obj = obj };

            yield return new NodeUpdateRequest { Key = "key", Obj = obj };
            yield return new NodeUpdateResponse { Obj = obj };

            yield return new NodeSyncRequest
            {
                NodeId = 0,
                Stores = new()
                {
                    { ConsistencyMode.Eventual, requestMsg },
                    { ConsistencyMode.Fifo, requestMsg},
                    { ConsistencyMode.FifoP1, requestMsg},
                    { ConsistencyMode.FifoP2,requestMsg},
                    { ConsistencyMode.FifoP3, requestMsg},
                }
            };

            yield return new NodeSyncResponse
            {
                NodeId = 0,
                Stores = new()
                {
                    {ConsistencyMode.Eventual, responseMsg},
                    {ConsistencyMode.Fifo, responseMsg},
                    {ConsistencyMode.FifoP1, responseMsg},
                    {ConsistencyMode.FifoP2, responseMsg},
                    {ConsistencyMode.FifoP3, responseMsg},
                }
            };
        }
    }
}
