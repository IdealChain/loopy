using Loopy.Data;
using Loopy.Enums;
using NUnit.Framework;

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
                { Priority.P0, 1 }, { Priority.P1, 4 }, { Priority.P2, 4 }, { Priority.P3, 8 }
            };
            var s = f.Strip();
            
            Assert.That(s[Priority.P0], Is.EqualTo(0));
            Assert.That(s[Priority.P1], Is.EqualTo(4));
            Assert.That(s[Priority.P2], Is.EqualTo(0));
            Assert.That(s[Priority.P3], Is.EqualTo(8));
            Assert.That(s.ContainsKey(Priority.P0), Is.False);
            Assert.That(s.ContainsKey(Priority.P2), Is.False);
        }

        [Test]
        public void TestFill()
        {
            var s = new FifoDistances { { Priority.P1, 4 }, { Priority.P3, 8 } };
            var f = s.Fill();
            
            Assert.That(f[Priority.P0], Is.EqualTo(1));
            Assert.That(f[Priority.P1], Is.EqualTo(4));
            Assert.That(f[Priority.P2], Is.EqualTo(4));
            Assert.That(f[Priority.P3], Is.EqualTo(8));
        }

        [Test]
        public void TestGetPredecessorId()
        {
            var p = new Dictionary<Priority, int>
            {
                { Priority.P0, 999 }, { Priority.P1, 996 }, { Priority.P2, 996 }, { Priority.P3, 992 }
            };

            var f = new FifoDistances(p, 1000);
            
            Assert.That(f.GetPredecessorId(Priority.P0, 1000), Is.EqualTo(999));
            Assert.That(f.GetPredecessorId(Priority.P1, 1000), Is.EqualTo(996));
            Assert.That(f.GetPredecessorId(Priority.P2, 1000), Is.EqualTo(996));
            Assert.That(f.GetPredecessorId(Priority.P3, 1000), Is.EqualTo(992));
        }

        [Test]
        public void TestGetSkippedUpdateIds()
        {
            var p = new Dictionary<Priority, int>
            {
                { Priority.P0, 999 }, { Priority.P1, 996 }, { Priority.P2, 996 }, { Priority.P3, 992 }
            };

            var f = new FifoDistances(p, 1000);
            
            Assert.That(f.GetSkippableUpdateIds(Priority.P0, 1000), Is.Empty);
            Assert.That(f.GetSkippableUpdateIds(Priority.P1, 1000), Is.EqualTo(new[] { 997, 998, 999 }));
            Assert.That(f.GetSkippableUpdateIds(Priority.P2, 1000), Is.EqualTo(new[] { 997, 998, 999 }));
            Assert.That(f.GetSkippableUpdateIds(Priority.P3, 1000), Is.EqualTo(new[] { 993, 994, 995, 996, 997, 998, 999 }));
        }
    }
}
