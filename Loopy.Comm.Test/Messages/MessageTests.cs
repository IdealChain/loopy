using Loopy.Comm.Messages;
using Loopy.Core.Enums;
using NetMQ;
using NUnit.Framework;

namespace Loopy.Comm.Test.Messages;

public class MessageTests
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
                Obj = new NdcObjectMsg
                {
                    DotValues = [(0, 0, "value", [1, 1, 1, 1])],
                    CausalContext = new() { CausalContext = [(0, 0)] },
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
