using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NLog;
using Object = Loopy.Data.Object;

namespace Loopy.Consistency;

public class CausalStore : IConsistencyStore
{
    private Node Node { get; }
    
    private readonly Map<NodeId, UpdateIdSet> CausalClock = new();

    private readonly Map<Key, Object> CausalStorage = new();

    private readonly HashSet<Key> DirtyKeys = new();

    public CausalStore(Node node) => Node = node;

    public void CheckMerge(Key k, Object o)
    {
        return;
        
        DirtyKeys.Add(k);
        Merge();
    }

    public Object Fetch(Key k, Priority p = default)
    {
        return Node.Fill(k, CausalStorage[k], CausalClock);
    }

    private void Merge()
    {
        var mergedKeys = new HashSet<Key>();
        foreach (var key in DirtyKeys)
        {
            using (ScopeContext.PushNestedState($"MergeCausal({key})"))
            {
                var f = Node.Fetch(key);
                var (vers, cc) = (f.DotValues, f.CausalContext);

                // check whether there's a gap between the causal context of the object and our causal visibility clock
                var nodesWithGaps = Enumerable.Where(f.CausalContext, p => p.Value - CausalClock[p.Key].Base > 1).Select(p => p.Key);
                if (nodesWithGaps.Any())
                {
                    Node.Logger.Trace("Causal gap, keeping hidden: {Nodes}", nodesWithGaps);
                    continue;
                }
                
                Node.Logger.Trace("Causal ok, merging: {Dots}", vers.Keys);
                Store(key, f);
                mergedKeys.Add(key);
            }
        }

        DirtyKeys.ExceptWith(mergedKeys);
    }

    private void Store(Key key, Object f)
    {
        var m = Node.Merge(Node.Fill(key, CausalStorage[key], CausalClock), f);
        var s = Node.Strip(m, CausalClock);
        var (vers, cc) = (s.DotValues, s.CausalContext);
            
        if (Enumerable.All<Value>(vers.Values, v => v.IsEmpty) && cc.Count == 0)
            CausalStorage.Remove(key);
        else
            CausalStorage[key] = s;
                    
        foreach (var (n, c) in vers.Keys)
            CausalClock[n].Add(c);
    }
}
