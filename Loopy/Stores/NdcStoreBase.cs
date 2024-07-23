using Loopy.Data;
using Loopy.Interfaces;

namespace Loopy.Stores;

/// <summary>
/// NDC framework store: NC, DKM, WM, NSK, ST
/// </summary>
internal abstract class NdcStoreBase : INdcStore
{
    private readonly NodeId _nodeId;
    private readonly INodeContext _context;

    protected NdcStoreBase(NodeId id, INodeContext context)
    {
        _nodeId = id;
        _context = context;
    }

    /// <summary>
    /// All dots from current and past versions seen by this node
    /// </summary>
    protected readonly NodeClock NodeClock = new();

    /// <summary>
    /// Maps dots of locally stored versions to keys -
    /// entries are removed when dots are known by every peer node
    /// </summary>
    protected readonly Map<Dot, Key> DotKeyMap = new();

    /// <summary>
    /// A cache of node clocks from every peer, including itself -
    /// in practice, only the base counter of every entry is saved
    /// </summary>
    protected readonly Map<NodeId, Map<NodeId, int>> Watermark = new();

    /// <summary>
    /// The keys of local objects with a non-empty causal context
    /// </summary>
    protected readonly HashSet<Key> NonStrippedKeys = new();

    /// <summary>
    /// Maps keys to objects
    /// </summary>
    protected readonly Map<Key, NdcObject> Storage = new();

    public NdcObject Fetch(Key k)
    {
        if (!Storage.TryGetValue(k, out var obj) || obj.IsEmpty)
            obj = new();

        return obj.Fill(NodeClock, _context.GetReplicaNodes(k));
    }

    protected virtual NdcObject Update(Key k, NdcObject o)
    {
        var f = Fetch(k);
        var m = o.Merge(f);
        Store(k, m);
        return m;
    }

    protected virtual void Store(Key k, NdcObject o)
    {
        o = o.Strip(NodeClock);

        // remove object if there are only null values left and cc is empty
        if (o.IsEmpty && o.CausalContext.Count == 0)
            Storage.Remove(k);
        else
            Storage[k] = o;

        // (a) add all version dots to the node clock and the dot-key-map
        foreach (var (n, c) in o.DotValues.Keys)
        {
            NodeClock[n].Add(c);
            DotKeyMap[(n, c)] = k;
        }

        // (b) add the key to the non-stripped key set if cc is not empty
        if (o.CausalContext.Count == 0)
            NonStrippedKeys.Remove(k);
        else
            NonStrippedKeys.Add(k);
    }

    public void StripCausality()
    {
        foreach (var k in NonStrippedKeys.ToList())
            Store(k, Storage[k]);
    }

    public virtual ModeSyncRequest GetSyncRequest() => new ModeSyncRequest { PeerClock = NodeClock };

    public virtual ModeSyncResponse SyncClock(NodeId peer, ModeSyncRequest request)
    {
        var response = new ModeSyncResponse { PeerClock = NodeClock };

        // get all keys from dots missing in the node p
        var missingKeys = new HashSet<Key>();
        foreach (var n in _context.GetPeerNodes(_nodeId).Intersect(_context.GetPeerNodes(peer)))
            foreach (var c in NodeClock[n].Except(request.PeerClock[n]))
                missingKeys.Add(DotKeyMap[(n, c)]);

        // get the missing objects from keys replicated by p
        foreach (var k in missingKeys)
        {
            if (_context.GetReplicaNodes(k).Contains(peer))
                response.MissingObjects.Add((k, Storage[k]));
        }

        return response;
    }

    public virtual void SyncRepair(NodeId peer, ModeSyncResponse response)
    {
        var peerClock = response.PeerClock;
        var missingObjects = response.MissingObjects;

        // update local objects with the missing objects
        foreach (var (k, o) in missingObjects)
            Update(k, o.Fill(peerClock, _context.GetReplicaNodes(k)));

        // merge p's node clock entry to close gaps
        NodeClock[peer].UnionWith(peerClock[peer]);

        // update the WM with new i and p clocks
        foreach (var n in peerClock.Keys.Intersect(_context.GetPeerNodes(_nodeId)))
            Watermark[peer][n] = Math.Max(Watermark[peer][n], peerClock[n].Base);

        foreach (var n in NodeClock.Keys)
            Watermark[_nodeId][n] = Math.Max(Watermark[_nodeId][n], NodeClock[n].Base);

        // remove entries known by all peers
        foreach (var (n, c) in DotKeyMap.Keys)
        {
            if (_context.GetPeerNodes(n).Min(m => Watermark[m][n]) >= c)
                DotKeyMap.Remove((n, c));
        }
    }
}
