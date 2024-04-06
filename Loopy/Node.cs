using System.Diagnostics;
using System.Text;

namespace Loopy;

/// <summary>
/// The NDC framework requires each node to maintain:
/// NC, DKM, WM, NSK, ST
/// </summary>
[DebuggerDisplay("Node {i}")]
public class Node
{
    public NodeId i;
    internal NodeContext Context { get; }

    public Node(NodeId id, NodeContext context)
    {
        i = id;
        Context = context;
    }

    /// <summary>
    /// All dots from current and past versions seen by this node
    /// </summary>
    public SafeDict<NodeId, UpdateIdSet> NodeClock = new();

    /// <summary>
    /// Maps dots of locally stored versions to keys -
    /// entries are removed when dots are known by every peer node
    /// </summary>
    public SafeDict<Dot, Key> DotKeyMap = new();

    /// <summary>
    /// A cache of node clocks from every peer, including itself -
    /// in practice, only the base counter of every entry is saved
    /// </summary>
    public SafeDict<NodeId, SafeDict<NodeId, int>> Watermark = new();

    /// <summary>
    /// The keys of local objects with a non-empty causal context
    /// </summary>
    public HashSet<Key> NonStrippedKeys = new();

    /// <summary>
    /// Maps keys to objects
    /// </summary>
    public SafeDict<Key, Object> Storage = new();

    public Object Fetch(Key k)
    {
        return Fill(k, Storage.GetValueOrDefault(k), NodeClock);
    }

    public void Store(Key k, Object o)
    {
        o = Strip(o, NodeClock);
        var (vers, cc) = o;

        // remove object if there are only null values left and cc is empty
        if (vers.Values.All(v => v.IsEmpty) && cc.Count == 0)
            Storage.Remove(k);
        else
            Storage[k] = o;

        // (a) add all version dots to the node clock and the dot-key-map
        foreach (var (n, c) in vers.Keys)
        {
            NodeClock[n].Add(c);
            DotKeyMap[(n, c)] = k;
        }

        // (b) add the key to the non-stripped key set if cc is not empty
        if (cc.Count == 0)
            NonStrippedKeys.Remove(k);
        else
            NonStrippedKeys.Add(k);
    }

    internal static Object Merge(Object o1, Object o2)
    {
        var o = new Object();

        // versions of each object not obsoleted by each other
        // (d,v) obsoleted when (d,v) not in vers and dot is in cc
        o.vers.MergeIn(o1.vers.IntersectBy(o2.vers.Keys, p => p.Key));
        o.vers.MergeIn(o1.vers.Where(p => !o2.cc.ContainsDot(p.Key)));
        o.vers.MergeIn(o2.vers.Where(p => !o1.cc.ContainsDot(p.Key)));

        // merged causal context: taking the maximum counter for common node ids
        o.cc.MergeIn(o1.cc);
        o.cc.MergeIn(o2.cc, (_, c1, c2) => Math.Max(c1, c2));

        return o;
    }

    public Object Update(Key k, Object o)
    {
        var f = Fetch(k);
        var m = Merge(o, f);
        Store(k, m);
        return m;
    }

    private Object Strip(Object o, SafeDict<NodeId, UpdateIdSet> nc)
    {
        var stripped = new Object(o);
        foreach (var n in nc.Keys)
        {
            if (stripped.cc[n] <= nc[n].Base)
                stripped.cc.Remove(n);
        }

        return stripped;
    }

    private Object Fill(Key k, Object o, SafeDict<NodeId, UpdateIdSet> nc)
    {
        var filled = new Object(o);
        foreach (var n in Context.GetReplicaNodes(k))
            filled.cc[n] = Math.Max(filled.cc[n], nc[n].Base);

        return filled;
    }

    public async Task StripCausality(TimeSpan stripInterval, CancellationToken cancellationToken)
    {
        var rand = new Random(37);
        await Task.Delay(rand.Next(10000), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var k in NonStrippedKeys)
                Store(k, Storage[k]);

            await Task.Delay(stripInterval, cancellationToken);
        }
    }

    public async Task AntiEntropy(TimeSpan syncInterval, CancellationToken cancellationToken)
    {
        // deterministic random peer choice from all peers except oneself
        var rand = new Random(17);
        var peerNodes = Context.GetPeerNodes(i).Where(j => j != i).ToArray();
        await Task.Delay(rand.Next(10000), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var p = peerNodes[rand.Next(peerNodes.Length)];
            var (pNodeClock, pMissingObjects) = await Context.GetNodeApi(p).SyncClock(i, NodeClock);
            SyncRepair(p, pNodeClock, pMissingObjects);

            await Task.Delay(syncInterval);
        }
    }

    public (SafeDict<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects) SyncClock(
        NodeId p, SafeDict<NodeId, UpdateIdSet> pNodeClock)
    {
        // get all keys from dots missing in the node p
        var missingKeys = new HashSet<Key>();
        foreach (var n in Context.GetPeerNodes(i).Intersect(Context.GetPeerNodes(p)))
        foreach (var c in NodeClock[n].Except(pNodeClock[n]))
            missingKeys.Add(DotKeyMap[(n, c)]);

        // get the missing objects from keys replicated by p
        var missingKeyObjects = new List<(Key, Object)>();
        foreach (var k in missingKeys)
        {
            if (Context.GetReplicaNodes(k).Contains(p))
                missingKeyObjects.Add((k, Storage[k]));
        }

        // remote sync_repair => return results instead of RPC
        return (NodeClock, missingKeyObjects);
    }

    public void SyncRepair(NodeId p, SafeDict<NodeId, UpdateIdSet> pNodeClock,
        IEnumerable<(Key, Object)> missingObjects)
    {
        // update local objects with the missing objects
        foreach (var (k, o) in missingObjects)
            Update(k, Fill(k, o, pNodeClock));

        // merge p's node clock entry to close gaps
        NodeClock[p].UnionWith(pNodeClock[p]);

        // update the WM with new i and p clocks
        foreach (var n in pNodeClock.Keys.Intersect(Context.GetPeerNodes(i)))
            Watermark[p][n] = Math.Max(Watermark[p][n], pNodeClock[n].Base);

        foreach (var n in NodeClock.Keys)
            Watermark[i][n] = Math.Max(Watermark[i][n], NodeClock[n].Base);

        // remove entries known by all peers
        foreach (var (n, c) in DotKeyMap.Keys)
        {
            if (Context.GetPeerNodes(n).Min(m => Watermark[m][n]) >= c)
                DotKeyMap.Remove((n, c));
        }
    }
}

/// <summary>
/// An object internally encodes a logical clock by tagging
/// every (concurrent) value with a dot and storing all
/// current versions (versions) and past versions (causal context) as dots
/// </summary>
public record struct Object
{
    public Object()
    {
    }

    public Object(Object obj = default)
    {
        if (obj.vers != null) vers = new SafeDict<Dot, Value>(obj.vers);
        if (obj.cc != null) cc = new SafeDict<NodeId, int>(obj.cc);
    }

    /// <summary>
    /// Concurrent values
    /// </summary>
    public SafeDict<Dot, Value> vers = new();

    /// <summary>
    /// Past versions (causal context)
    /// </summary>
    public SafeDict<NodeId, int> cc = new();

    public readonly void Deconstruct(out SafeDict<Dot, Value> vers, out SafeDict<NodeId, int> cc)
    {
        vers = this.vers;
        cc = this.cc;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        if (vers.Count > 0)
        {
            foreach (var (d, v) in vers)
            {
                if (sb.Length > 0)
                    sb.Append(", ");

                sb.AppendFormat("{0}={1}", d, v);
            }
        }
        else
        {
            sb.Append("-");
        }

        sb.Append(" / Context:");
        if (cc.Count > 0)
        {
            foreach (var (n, c) in cc)
                sb.AppendFormat(" {0}={1}", n, c);
        }
        else
        {
            sb.Append("-");
        }

        return sb.ToString();
    }
}