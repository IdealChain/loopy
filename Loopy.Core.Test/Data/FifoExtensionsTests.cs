using Loopy.Core.Data;
using Loopy.Core.Enums;
using NUnit.Framework;

namespace Loopy.Core.Test.Data
{
    [TestFixture]
    public class FifoExtensionsTests
    {
        [Test]
        public void TestStrip()
        {
            int[] fd = [1, 4, 4, 8];
            var map = fd.StripFifoDistances();

            Assert.That(map[Priority.P0], Is.EqualTo(0));
            Assert.That(map[Priority.P1], Is.EqualTo(4));
            Assert.That(map[Priority.P2], Is.EqualTo(0));
            Assert.That(map[Priority.P3], Is.EqualTo(8));
            Assert.That(map.ContainsKey(Priority.P0), Is.False);
            Assert.That(map.ContainsKey(Priority.P2), Is.False);
        }

        [Test]
        public void TestFill()
        {
            var map = new Map<Priority, int> { { Priority.P1, 4 }, { Priority.P3, 8 } };
            var fd = map.FillFifoDistances();

            Assert.That(fd[0], Is.EqualTo(1));
            Assert.That(fd[1], Is.EqualTo(4));
            Assert.That(fd[2], Is.EqualTo(4));
            Assert.That(fd[3], Is.EqualTo(8));
        }

        [Test]
        public void TestGetPredecessorId()
        {
            int[] fp = [999, 996, 996, 992];
            var fd = fp.Select(pre => 1000 - pre).ToArray();

            Assert.That(fd.GetFifoPredecessor(1000, Priority.P0), Is.EqualTo(999));
            Assert.That(fd.GetFifoPredecessor(1000, Priority.P1), Is.EqualTo(996));
            Assert.That(fd.GetFifoPredecessor(1000, Priority.P2), Is.EqualTo(996));
            Assert.That(fd.GetFifoPredecessor(1000, Priority.P3), Is.EqualTo(992));
        }

        [Test]
        public void TestGetSkippedUpdateIds()
        {
            int[] fp = [999, 996, 996, 992];
            var fd = fp.Select(pre => 1000 - pre).ToArray();

            Assert.That(fd.GetFifoSkippableUpdates(1000, Priority.P0), Is.Empty);
            Assert.That(fd.GetFifoSkippableUpdates(1000, Priority.P1), Is.EqualTo(new[] { 997, 998, 999 }));
            Assert.That(fd.GetFifoSkippableUpdates(1000, Priority.P2), Is.EqualTo(new[] { 997, 998, 999 }));
            Assert.That(fd.GetFifoSkippableUpdates(1000, Priority.P3), Is.EqualTo(new[] { 993, 994, 995, 996, 997, 998, 999 }));
        }
    }
}
