using Loopy.Data;
using NLog;
using Object = Loopy.Data.Object;

namespace Loopy
{
    public partial class Node
    {
        public readonly Map<NodeId, UpdateIdSet> FifoNodeClock = new();

        public readonly Map<Key, Object> FifoStorage = new();

        public readonly HashSet<Key> FifoToCheck = new();

        private void CheckFifo(Key k)
        {
            FifoToCheck.Add(k);
            MergeFifo();
        }

        private void MergeFifo()
        {
            var mergedKeys = new HashSet<Key>();
            foreach (var key in FifoToCheck)
            {
                using (ScopeContext.PushNestedState($"MergeFifo({key})"))
                {
                    var f = Fetch(key);
                    var (vers, cc) = (f.DotValues, f.CausalContext);

                    // FIFO: we must not apply the value if there is any gap in the eventual node clock,
                    // i.e., ANY writes of the originating node were not seen
                    var dotsWithGaps = vers.Keys.Where(d => f.CausalContext[d.NodeId] - NodeClock[d.NodeId].Base > 1);
                    if (dotsWithGaps.Any())
                    {
                        Logger.Trace("FIFO gap, keeping hidden: {Dots}", dotsWithGaps);
                    }
                    else
                    {
                        Logger.Trace("FIFO ok, merging: {Dots}", vers.Keys);
                        StoreFifo(key, f);
                        mergedKeys.Add(key);
                    }
                }
            }

            FifoToCheck.ExceptWith(mergedKeys);
        }

        private void StoreFifo(Key key, Object f)
        {
            var m = Merge(Fill(key, FifoStorage[key], FifoNodeClock), f);
            var s = Strip(m, FifoNodeClock);
            var (vers, cc) = (s.DotValues, s.CausalContext);
            
            if (vers.Values.All(v => v.IsEmpty) && cc.Count == 0)
                FifoStorage.Remove(key);
            else
                FifoStorage[key] = s;
                    
            foreach (var (n, c) in vers.Keys)
                FifoNodeClock[n].Add(c);
        }

        public Object FetchFifo(Key k) => Fill(k, FifoStorage[k], FifoNodeClock);
    }
}