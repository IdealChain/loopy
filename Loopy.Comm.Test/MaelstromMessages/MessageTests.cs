using Loopy.Comm.Extensions;
using Loopy.Comm.MaelstromMessages;
using Loopy.Comm.NdcMessages;
using Loopy.Comm.Sockets;
using Loopy.Core.Enums;
using NUnit.Framework;

namespace Loopy.Comm.Test.MaelstromMessages;

[TestFixture]
internal class MessageTests
{
    [Test]
    [TestCaseSource(nameof(MessageSource))]
    public void TestJsonRoundtrip(MessageBase msg)
    {
        var e1 = new Envelope { src = "src", dest = "dest", body = msg };

        var line = JsonSocket<Envelope>.Serialize(e1);
        TestContext.Out.WriteLine($"{line.Length} Chars");
        TestContext.Out.WriteIndentedJson(line);

        var e2 = JsonSocket<Envelope>.Deserialize(line);
        Assert.That(e2, Is.Not.Null);
        Assert.That(e2.src, Is.EqualTo(e1.src));
        Assert.That(e2.dest, Is.EqualTo(e1.dest));
        Assert.That(e2.body.GetType(), Is.EqualTo(msg.GetType()));
    }

    private static IEnumerable<MessageBase> MessageSource
    {
        get
        {
            yield return new ErrorResponse(ErrorCode.KeyDoesNotExist, "no key");
            yield return new InitRequest("n1", ["n2", "n3"]);
            yield return new InitOkResponse();
            yield return new ReadRequest("key");
            yield return new ReadOkResponse("1");
            yield return new WriteRequest("key", "2");
            yield return new WriteOkResponse();
            yield return new CasRequest("key", "3", "4");
            yield return new CasOkResponse();
            yield return new WrappedNdcRequest(new NodeFetchRequest { Key = "1", Mode = ConsistencyMode.Fifo });
            yield return new WrappedNdcResponse(new NodeFetchResponse { Obj = null });
        }
    }
}
