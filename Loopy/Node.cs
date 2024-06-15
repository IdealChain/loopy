using Loopy.Consistency;
using System.Diagnostics;
using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NLog;
using Object = Loopy.Data.Object;

namespace Loopy;

/// <summary>
/// The NDC framework requires each node to maintain:
/// NC, DKM, WM, NSK, ST
/// </summary>
[DebuggerDisplay("Node {Id.Id}")]
public partial class Node : IClientApi
{
    private NodeId i;

    public NodeId Id => i;
    public ILogger Logger { get; }
    public INodeContext Context { get; }

    public Node(NodeId id, INodeContext context)
    {
        i = id;
        Logger = LogManager.GetLogger(id.ToString());
        Context = context;

        ConsistencyStores[ConsistencyMode.Eventual] = new EventualStore(this);
        ConsistencyStores[ConsistencyMode.Fifo] = new FifoStore(this);
    }

    /// <summary>
    /// Lock to limit concurrent access to node's data
    /// </summary>
    public readonly AwaitableLock NodeLock = new();

    /// <summary>
    /// All dots from current and past versions seen by this node
    /// </summary>
    internal readonly Map<NodeId, UpdateIdSet> NodeClock = new();

    /// <summary>
    /// Fifo predecessor (preceeding update id with equal-or-higher priority)
    /// </summary>
    internal readonly Map<Priority, int> FifoPriorityPredecessor = new();

    /// <summary>
    /// Maps dots of locally stored versions to keys -
    /// entries are removed when dots are known by every peer node
    /// </summary>
    internal readonly Map<Dot, Key> DotKeyMap = new();

    /// <summary>
    /// A cache of node clocks from every peer, including itself -
    /// in practice, only the base counter of every entry is saved
    /// </summary>
    internal readonly Map<NodeId, Map<NodeId, int>> Watermark = new();

    /// <summary>
    /// The keys of local objects with a non-empty causal context
    /// </summary>
    internal readonly HashSet<Key> NonStrippedKeys = new();

    /// <summary>
    /// Maps keys to objects
    /// </summary>
    internal readonly Map<Key, Object> Storage = new();

    private readonly Dictionary<ConsistencyMode, IConsistencyStore> ConsistencyStores = new();

    public Object Fetch(Key k, ConsistencyMode mode = ConsistencyMode.Eventual)
    {
        var modePart = (ConsistencyMode)((int)mode & 0x0F);
        var prioPart = (Priority)(((int)mode & 0xF0) >> 4);
        return ConsistencyStores[modePart].Fetch(k, prioPart);
    }

    private void Store(Key k, Object o)
    {
        o = Strip(o, NodeClock);
        var (vers, cc) = (o.DotValues, o.CausalContext);

        // remove object if there are only null values left and cc is empty
        if (o.IsEmpty && cc.Count == 0)
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
        o.DotValues.MergeIn(o1.DotValues.IntersectBy(o2.DotValues.Keys, p => p.Key));
        o.DotValues.MergeIn(o1.DotValues.Where(p => !o2.CausalContext.Contains(p.Key)));
        o.DotValues.MergeIn(o2.DotValues.Where(p => !o1.CausalContext.Contains(p.Key)));

        // same for fifo distances
        o.FifoDistances.MergeIn(o1.FifoDistances.IntersectBy(o2.FifoDistances.Keys, p => p.Key));
        o.FifoDistances.MergeIn(o1.FifoDistances.Where(p => !o2.CausalContext.Contains(p.Key)));
        o.FifoDistances.MergeIn(o2.FifoDistances.Where(p => !o1.CausalContext.Contains(p.Key)));

        // merged causal context: taking the maximum counter for common node ids
        o.CausalContext.MergeIn(o1.CausalContext);
        o.CausalContext.MergeIn(o2.CausalContext, (_, c1, c2) => Math.Max(c1, c2));

        return o;
    }

    public Object Update(Key k, Object o)
    {
        var f = Fetch(k);
        var m = Merge(o, f);
        Store(k, m);

        // notify all consistency stores
        foreach (var cs in ConsistencyStores.Values)
            cs.CheckMerge(k, m);

        return m;
    }

    /// <summary>
    /// Removes per-key causal context that is already covered by the given node clock 
    /// </summary>
    internal Object Strip(Object o, Map<NodeId, UpdateIdSet> nc)
    {
        var s = new Object(o.DotValues, o.CausalContext, o.FifoDistances);
        foreach (var n in nc.Keys)
        {
            if (s.CausalContext[n] <= nc[n].Base)
                s.CausalContext.Remove(n);
        }

        return s;
    }

    /// <summary>
    /// Fills back per-key causal context from the given node clock
    /// </summary>
    internal Object Fill(Key k, Object o, Map<NodeId, UpdateIdSet> nc)
    {
        var f = new Object(o.DotValues, o.CausalContext, o.FifoDistances);
        foreach (var n in Context.GetReplicaNodes(k))
            f.CausalContext[n] = Math.Max(f.CausalContext[n], nc[n].Base);

        return f;
    }
}
