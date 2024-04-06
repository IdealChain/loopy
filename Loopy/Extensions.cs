namespace Loopy;

public static class Extensions
{
    public static void MergeIn<TK, TV>(this Dictionary<TK, TV> dict,
        IEnumerable<KeyValuePair<TK, TV>> pairs,
        Func<TK, TV, TV, TV>? conflictResolver = null) where TK : notnull
    {
        MergeIn(dict, pairs.Select(kv => (kv.Key, kv.Value)), conflictResolver);
    }
    
    public static void MergeIn<TK, TV>(this Dictionary<TK, TV> dict,
        IEnumerable<(TK, TV)> tuples,
        Func<TK, TV, TV, TV>? conflictResolver = null) where TK : notnull
    {
        foreach (var (k,v) in tuples)
        {
            if (dict.TryGetValue(k, out var existing) && !Equals(v, existing))
            {
                if (conflictResolver == null)
                    throw new InvalidOperationException("Conflict: resolver required");
                
                dict[k] = conflictResolver(k, existing, v);
            }
            else
            {
                dict[k] = v;
            }
        }
    }

    public static bool ContainsDot(this SafeDict<NodeId, int> cc, Dot dot)
    {
        return cc[dot.NodeId] >= dot.UpdateId;
    }
}