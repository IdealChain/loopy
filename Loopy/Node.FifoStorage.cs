using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NLog;
using Object = Loopy.Data.Object;

namespace Loopy
{
    public partial class Node
    {
        public readonly Dictionary<Priority, FifoStore> FifoStores =
            Enum.GetValues<Priority>().ToDictionary(p => p, p => new FifoStore(p));

        private void CheckFifo(Key k)
        {
            // eventual store contents for key changed; check all separate fifo stores
            foreach (var s in FifoStores.Values)
            {
                s.FifoToCheck.Add(k);
                s.MergeFifo(this);
            }
        }

        public Object FetchFifo(Key k, Priority p)
        {
            return FifoStores.TryGetValue(p, out var s) ? s.FetchFifo(this, k) : new Object();
        }

        public class FifoStore
        {
            public Priority Prio { get; }

            public readonly Map<NodeId, UpdateIdSet> FifoNodeClock = new();

            public readonly Map<Key, Object> FifoStorage = new();

            public readonly HashSet<Key> FifoToCheck = new();            

            public FifoStore(Priority prio) => Prio = prio;

            public void MergeFifo(Node node)
            {
                var mergedKeys = new HashSet<Key>();
                foreach (var key in FifoToCheck)
                {
                    using (ScopeContext.PushNestedState($"MergeFifo({key}, {Prio})"))
                    {
                        var f = node.Fetch(key);
                        var (vers, _, fb) = (f.DotValues, f.CausalContext, f.FifoBarriers);

                        // FIFO: we must not apply the value if there is any gap in the eventual node clock,
                        // i.e., ANY writes of the originating node were not seen
                        // var fifoGaps = vers.Keys.Where(d => f.CausalContext[d.NodeId] - Node.NodeClock[d.NodeId].Base > 1);
                        var fifoGaps = vers.Keys.Where(d => node.NodeClock[d.NodeId].Base < fb[Prio]);
                        if (fifoGaps.Any())
                        {
                            node.Logger.Trace("FIFO gap, keeping hidden: {Dots}", fifoGaps);
                        }
                        else
                        {
                            node.Logger.Trace("FIFO ok, merging: {Dots}", vers.Keys);
                            StoreFifo(node, key, f);
                            mergedKeys.Add(key);
                        }
                    }
                }

                FifoToCheck.ExceptWith(mergedKeys);
            }

            private void StoreFifo(Node node, Key key, Object f)
            {
                var m = Merge(node.Fill(key, FifoStorage[key], FifoNodeClock), f);
                var s = node.Strip(m, FifoNodeClock);
                var (vers, cc) = (s.DotValues, s.CausalContext);

                if (vers.Values.All(v => v.IsEmpty) && cc.Count == 0)
                    FifoStorage.Remove(key);
                else
                    FifoStorage[key] = s;

                foreach (var (n, c) in vers.Keys)
                    FifoNodeClock[n].Add(c);
            }

            public Object FetchFifo(Node node, Key k) => node.Fill(k, FifoStorage[k], FifoNodeClock);
        }
    }
}
