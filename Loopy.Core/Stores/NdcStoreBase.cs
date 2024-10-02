using Loopy.Core.Data;
using Loopy.Core.Interfaces;

namespace Loopy.Core.Stores;

/// <summary>
/// NDC framework store: NC, DKM, WM, NSK, ST
/// </summary>
internal abstract class NdcStoreBase(INodeContext context) : INdcStore
{
    protected INodeContext Context { get; } = context;

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

        return obj.Fill(NodeClock, Context.ReplicationStrategy.GetReplicaNodes(k));
    }

    public event EventHandler<(Key, NdcObject)>? ValueChanged;

    protected virtual NdcObject Update(Key k, NdcObject o)
    {
        var f = Fetch(k);
        var m = o.Merge(f);
        Store(k, m);
        return m;
    }

    protected virtual void Store(Key k, NdcObject o)
    {
        // capture existing dots for change notification
        var dotSet = Storage.TryGetValue(k, out var existingObject) ?
            new HashSet<Dot>(existingObject.DotValues.Keys) : new HashSet<Dot>();

        var unstripped = o;
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

        // suppress event if the dots contained in the object did not change
        if (ValueChanged != null)
        {
            dotSet.SymmetricExceptWith(o.DotValues.Keys);
            if (dotSet.Count > 0)
                ValueChanged.Invoke(this, (k, unstripped));
        }
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
        foreach (var n in Context.ReplicationStrategy.GetPeerNodes(Context.NodeId).Intersect(Context.ReplicationStrategy.GetPeerNodes(peer)))
            foreach (var c in NodeClock[n].Except(request.PeerClock[n]))
                if (DotKeyMap.TryGetValue((n, c), out var key))
                    missingKeys.Add(key);

        // get the missing objects from keys replicated by p
        foreach (var k in missingKeys)
        {
            if (Context.ReplicationStrategy.GetReplicaNodes(k).Contains(peer))
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
            Update(k, o.Fill(peerClock, Context.ReplicationStrategy.GetReplicaNodes(k)));

        // merge p's node clock entry to close gaps
        NodeClock[peer].UnionWith(peerClock[peer]);

        // update the WM with new i and p clocks
        foreach (var n in peerClock.Keys.Intersect(Context.ReplicationStrategy.GetPeerNodes(Context.NodeId)))
            Watermark[peer][n] = Math.Max(Watermark[peer][n], peerClock[n].Base);

        foreach (var n in NodeClock.Keys)
            Watermark[Context.NodeId][n] = Math.Max(Watermark[Context.NodeId][n], NodeClock[n].Base);

        // remove entries known by all peers
        foreach (var (n, c) in DotKeyMap.Keys)
        {
            if (Context.ReplicationStrategy.GetPeerNodes(n).Min(m => Watermark[m][n]) >= c)
                DotKeyMap.Remove((n, c));
        }
    }
}
