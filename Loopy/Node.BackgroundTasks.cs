using Loopy.Consistency;
using Loopy.Data;
using Loopy.Enums;
using NLog;
using System.Diagnostics;
using Object = Loopy.Data.Object;

namespace Loopy;

public partial class Node
{
    public Task StripCausality()
    {
        using (ScopeContext.PushNestedState("StripCausality"))
        {
            foreach (var k in NonStrippedKeys.ToList())
                Store(k, Storage[k]);
        }

        return Task.CompletedTask;
    }

    public async Task AntiEntropy(NodeId peer, CancellationToken cancellationToken = default)
    {
        Trace.Assert(peer != Id);

        var prios = Enum.GetValues<Priority>().ToArray();
        var peerApi = Context.GetNodeApi(peer);

        using (ScopeContext.PushNestedState($"AntiEntropy({Id}<-{peer})"))
        {
            Logger.Trace("requesting missing objects");
            var (pNodeClock, pMissingObjects) = await peerApi.SyncClock(i, NodeClock, cancellationToken);

            // also fetch missing Fifo objects
            var d = new Map<Priority, (Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)>();
            var fifoStore = (FifoStore)ConsistencyStores[ConsistencyMode.Fifo];
            foreach (var prio in prios)
                d[prio] = await peerApi.SyncFifoClock(i, prio, fifoStore.GetClock(prio), cancellationToken);

            SyncRepair(peer, pNodeClock, pMissingObjects);

            // also repair Fifo stores
            foreach (var prio in prios)
                fifoStore.SyncRepair(peer, prio, d[prio].NodeClock, d[prio].missingObjects);
        }
    }

    public (Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects) SyncClock(
        NodeId peer, Map<NodeId, UpdateIdSet> peerNodeClock)
    {
        using (ScopeContext.PushNestedState($"SyncClock({i}->{peer})"))
        {
            // get all keys from dots missing in the node p
            var missingKeys = new HashSet<Key>();
            foreach (var n in Context.GetPeerNodes(i).Intersect(Context.GetPeerNodes(peer)))
                foreach (var c in NodeClock[n].Except(peerNodeClock[n]))
                    missingKeys.Add(DotKeyMap[(n, c)]);

            // get the missing objects from keys replicated by p
            var missingKeyObjects = new List<(Key, Object)>();
            foreach (var k in missingKeys)
            {
                if (Context.GetReplicaNodes(k).Contains(peer))
                    missingKeyObjects.Add((k, Storage[k]));
            }

            // remote sync_repair => return results instead of RPC
            Logger.Trace("comparing {NC}: {Missing} objects for {N}",
                peerNodeClock.ValuesToString(), missingKeyObjects.Count, peer);
            return (NodeClock, missingKeyObjects);
        }
    }

    private void SyncRepair(NodeId peer, Map<NodeId, UpdateIdSet> peerNodeClock, List<(Key, Object)> missingObjects)
    {
        using (ScopeContext.PushNestedState($"SyncRepair({peer})"))
        {
            Logger.Trace("applying {NC}: {Missing} objects from {N}",
                peerNodeClock.ValuesToString(), missingObjects.Count, peer);

            // update local objects with the missing objects
            foreach (var (k, o) in missingObjects)
                Update(k, Fill(k, o, peerNodeClock));

            // merge p's node clock entry to close gaps
            NodeClock[peer].UnionWith(peerNodeClock[peer]);

            // update the WM with new i and p clocks
            foreach (var n in peerNodeClock.Keys.Intersect(Context.GetPeerNodes(i)))
                Watermark[peer][n] = Math.Max(Watermark[peer][n], peerNodeClock[n].Base);

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

    public (Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects) SyncFifoClock(
        NodeId p, Priority prio, Map<NodeId, UpdateIdSet> pNodeClock)
    {
        var fifoStore = (FifoStore)ConsistencyStores[ConsistencyMode.Fifo];
        return fifoStore.SyncClock(p, prio, pNodeClock);
    }
}
