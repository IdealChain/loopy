using Loopy.Data;
using NLog;
using Object = Loopy.Data.Object;

namespace Loopy;

public partial class Node
{
    public async Task StripCausality(TimeSpan stripInterval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (await NodeLock.Enter(cancellationToken))
            using (ScopeContext.PushNestedState("StripCausality"))
            {
                Logger.Trace("stripping...");
                foreach (var k in NonStrippedKeys.ToList())
                    Store(k, Storage[k]);
            }

            await Task.Delay(stripInterval, cancellationToken).ContinueWith(_ => { });
        }
    }

    public async Task AntiEntropy(TimeSpan syncInterval, CancellationToken cancellationToken)
    {
        // random peer choice from all peers except oneself
        var rand = new Random(i.Id);
        var peerNodes = Context.GetPeerNodes(i).Where(j => j != i).ToArray();

        while (!cancellationToken.IsCancellationRequested)
        {
            using (ScopeContext.PushNestedState($"AntiEntropy"))
            {
                var p = peerNodes[rand.Next(peerNodes.Length)];
                Logger.Trace("syncing with {Node}...", p);
                var (pNodeClock, pMissingObjects) = await Context.GetNodeApi(p).SyncClock(i, NodeClock);
                
                using (await NodeLock.Enter(cancellationToken))
                    SyncRepair(p, pNodeClock, pMissingObjects);
            }

            await Task.Delay(syncInterval, cancellationToken).ContinueWith(_ => { });
        }
    }

    public (Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects) SyncClock(
        NodeId p, Map<NodeId, UpdateIdSet> pNodeClock)
    {
        using (ScopeContext.PushNestedState($"SyncClock({p})"))
        {
            Logger.Trace("syncing: {NC}", pNodeClock.ValuesToString());

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
            Logger.Trace("returning: {Missing} missing objects for {N}", missingKeyObjects.Count, p);
            return (NodeClock, missingKeyObjects);
        }
    }

    private void SyncRepair(NodeId p, Map<NodeId, UpdateIdSet> pNodeClock, List<(Key, Object)> missingObjects)
    {
        using (ScopeContext.PushNestedState($"SyncRepair({p})"))
        {
            Logger.Trace("applying {NC}: {Missing} objects", pNodeClock.ValuesToString(), missingObjects.Count);

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
}
