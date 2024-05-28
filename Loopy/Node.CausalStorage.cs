using Loopy.Data;
using NLog;
using Object = Loopy.Data.Object;

namespace Loopy
{
    public partial class Node
    {
        public readonly Map<NodeId, UpdateIdSet> CausalNodeClock = new();
        
        public readonly Map<Key, Object> CausalStorage = new();

        public readonly HashSet<Key> CausalToCheck = new();

        private void CheckCausal(Key k)
        {
            CausalToCheck.Add(k);
            MergeCausal();
        }

        private void MergeCausal()
        {
            var mergedKeys = new HashSet<Key>();
            foreach (var key in CausalToCheck)
            {
                using (ScopeContext.PushNestedState($"MergeCausal({key})"))
                {
                    var f = Fetch(key);
                    var (vers, cc) = (f.DotValues, f.CausalContext);

                    // Causal
                    var nodesWithGaps = f.CausalContext.Where(p => p.Value - NodeClock[p.Key].Base > 1).Select(p => p.Key);
                    if (nodesWithGaps.Any())
                    {
                        Logger.Trace("Causal gap, keeping hidden: {Nodes}", nodesWithGaps);
                    }
                    else
                    {
                        Logger.Trace("Causal ok, merging: {Dots}", vers.Keys);
                        StoreCausal(key, f);
                        mergedKeys.Add(key);
                    }
                }
            }

            CausalToCheck.ExceptWith(mergedKeys);
        }

        private void StoreCausal(Key key, Object f)
        {
            var m = Merge(Fill(key, CausalStorage[key], CausalNodeClock), f);
            var s = Strip(m, CausalNodeClock);
            var (vers, cc) = (s.DotValues, s.CausalContext);
            
            if (vers.Values.All(v => v.IsEmpty) && cc.Count == 0)
                CausalStorage.Remove(key);
            else
                CausalStorage[key] = s;
                    
            foreach (var (n, c) in vers.Keys)
                CausalNodeClock[n].Add(c);
        }

        public Object FetchCausal(Key k) => Fill(k, CausalStorage[k], CausalNodeClock);
    }
}
