using Loopy.Data;
using Loopy.Enums;

namespace Loopy.Test.Data
{
    [TestFixture]
    public class FifoDistancesTest
    {
        [Test]
        public void TestStrip()
        {
            var f = new FifoDistances
            {
                { Priority.Bulk, 1 }, { Priority.Low, 4 }, { Priority.Normal, 4 }, { Priority.High, 8 }
            };
            var s = f.Strip();
            
            Assert.That(s[Priority.Bulk], Is.EqualTo(0));
            Assert.That(s[Priority.Low], Is.EqualTo(4));
            Assert.That(s[Priority.Normal], Is.EqualTo(0));
            Assert.That(s[Priority.High], Is.EqualTo(8));
            Assert.That(s.ContainsKey(Priority.Bulk), Is.False);
            Assert.That(s.ContainsKey(Priority.Normal), Is.False);
        }

        [Test]
        public void TestFill()
        {
            var s = new FifoDistances { { Priority.Low, 4 }, { Priority.High, 8 } };
            var f = s.Fill();
            
            Assert.That(f[Priority.Bulk], Is.EqualTo(1));
            Assert.That(f[Priority.Low], Is.EqualTo(4));
            Assert.That(f[Priority.Normal], Is.EqualTo(4));
            Assert.That(f[Priority.High], Is.EqualTo(8));
        }
    }
}
