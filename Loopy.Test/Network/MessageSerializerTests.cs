using Loopy.Comm.Messages;
using Loopy.Enums;
using NetMQ;

namespace Loopy.Test.Network;

public class MessageSerializerTests
{
    [Test]
    public void TestGetProtoDefinitions()
    {
        TestContext.Out.WriteLine(MessageSerializer.GetProtoDefinitions());
    }

    private static IEnumerable<IMessage> MessageSource
    {
        get
        {
            yield return new ClientGetRequest { Key = "key" };
            yield return new ClientGetResponse { Values = ["value"], CausalContext = new() };

            yield return new ClientPutRequest { Key = "key", Value = "value" };
            yield return new ClientPutResponse { Success = true };

            yield return new NodeFetchRequest { Key = "key", Mode = ConsistencyMode.Fifo };
            yield return new NodeFetchResponse
            {
                Obj = new ObjectMsg
                {
                    DotValues = new() { (0, 0, "value") },
                    CausalContext = new() { CausalContext = new() { (0, 0) } },
                    FifoBarriers = new() { (1, 2) },
                }
            };
        }
    }

    [Test]
    [TestCaseSource(nameof(MessageSource))]
    public void TestSerializeMsg(IMessage msg)
    {
        var mq = new NetMQMessage();
        MessageSerializer.Serialize(mq, msg);

        TestContext.Out.WriteLine("{0} Bytes", mq.Sum(x => x.MessageSize));
        TestContext.Out.WriteLine(mq);

        var deserialized = MessageSerializer.Deserialize(mq);
        Assert.That(deserialized.GetType(), Is.EqualTo(msg.GetType()));
    }
}
