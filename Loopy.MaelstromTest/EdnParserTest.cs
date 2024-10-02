using NUnit.Framework;

namespace Loopy.MaelstromTest;

[TestFixture]
public class EdnParserTest()
{
    [Test]
    public void TestParseInit()
    {
        var edn = """{:id 1, :src "c1", :dest "n3", :body {:type "init", :node_id "n3", :node_ids ["n0" "n1" "n2" "n3"], :msg_id 1}}""";
        Assert.That(EdnParser.TryParse(edn, out var msg));
        Assert.That(msg, Is.Not.Null);
    }

    [Test]
    public void TestParseWrite()
    {
        var edn = """{:id 22, :src "c19", :dest "n0", :body {:key 0, :value 16, :type "write", :msg_id 1}}""";
        Assert.That(EdnParser.TryParse(edn, out var msg));
        Assert.That(msg?["id"]?.GetValue<int>(), Is.EqualTo(22));
        Assert.That(msg?["src"]?.GetValue<string>(), Is.EqualTo("c19"));
    }
}
