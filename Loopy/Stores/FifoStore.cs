using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NLog;
using System.Runtime.CompilerServices;

namespace Loopy.Stores;

internal class FifoStore : NdcStoreBase, INdcStore
{
    private readonly Node _node;
    private readonly Priority _minPrio;
    private readonly Map<NodeId, SortedList<int, (Key key, NdcObject o)>> _pending = new();
    private bool _isSynchronizing;

    public FifoStore(Node node, Priority minPrio) : base(node.Id, node.Context)
    {
        _node = node;
        _minPrio = minPrio;
    }

    public void ProcessUpdate(Key k, NdcObject o)
    {
        using var _ = ScopeContext.PushNestedState($"UpdateFifo({k}, {_minPrio})");

        // if the key's priority is below our level, it is filtered and must be ignored
        if (k.Priority < _minPrio)
            return;

        // special handling for anti-entropy synced deletion: ignore empty object
        // (as the dot values are already received empty, we do not know node/update ID of the delete operation)
        if (o.DotValues.Count == 0)
        {
            _node.Logger.Trace("ignoring empty object [deletion]");
            return;
        }

        // group dots into immediately applicable (no gaps) and pending (gaps) sets, ignoring already applied ones
        var applicableDots = o.DotValues.Keys
            .Where(d => !NodeClock[d.NodeId].Contains(d.UpdateId))
            .ToLookup(d => NodeClock[d.NodeId].Base >= o.FifoDistances[d].GetPredecessorId(_minPrio, d.UpdateId));

        // short circuit the common case of no gaps: we can apply immediately, just as in the eventual store
        if (!applicableDots[false].Any())
        {
            Update(k, o);
            CheckPending(o.DotValues.Keys.Select(d => d.NodeId).Distinct());
            return;
        }

        // otherwise: immedatiely merge the applicable objects, schedule the rest for later
        if (applicableDots[true].Any())
            Update(k, o.Split(applicableDots[true]));

        foreach (var dot in applicableDots[false])
        {
            if (_pending[dot.NodeId].ContainsKey(dot.UpdateId))
                continue;

            _node.Logger.Trace("Pending [fifo gap]: {Dot}", dot);
            _pending[dot.NodeId].Add(dot.UpdateId, (k, o.Split([dot])));
        }

        CheckPending(o.DotValues.Keys.Select(d => d.NodeId).Distinct());
    }

    private void CheckPending(IEnumerable<NodeId> nodes)
    {
        foreach (var n in nodes)
        {
            if (!_pending.TryGetValue(n, out var p))
                continue;

            var merged = new List<Dot>();
            foreach (var (u, (k, o)) in p)
            {
                var d = new Dot(n, u);
                if (NodeClock[d.NodeId].Base >= o.FifoDistances[d].GetPredecessorId(_minPrio, u))
                {
                    _node.Logger.Trace("Merging [no fifo gap]: {Dot}={Value}", d, o.DotValues[d]);
                    Update(k, o);
                    merged.Add(d);
                }
            }

            if (merged.Count > 0)
            {
                foreach (var d in merged)
                    p.Remove(d.UpdateId);

                if (p.Count == 0)
                    _pending.Remove(n);
            }
        }
    }

    protected override void Store(Key k, NdcObject o)
    {
        // fill in update IDs that are allowed to be skipped
        foreach (var d in o.DotValues.Keys)
            NodeClock[d.NodeId].UnionWith(o.FifoDistances[d].GetSkippableUpdateIds(_minPrio, d.UpdateId));

        base.Store(k, o);

    }

    protected override NdcObject Update(Key k, NdcObject o)
    {
        var m = base.Update(k, o);

        // except temporary during anti-entropy sync...
        if (_isSynchronizing)
            return m;

        // gaps are never allowed to be stored
        foreach (var d in o.DotValues.Keys)
        {
            if (NodeClock[d.NodeId].Bitmap.Any())
                _node.Logger.Warn("FIFO condition violated: gaps for {Peer} {Updates}", d.NodeId, NodeClock[d.NodeId]);
        }

        return m;
    }

    public override void SyncRepair(NodeId peer, NodeClock peerClock, List<(Key, NdcObject)> missingObjects)
    {
        // update local objects with the missing FIFO objects
        try
        {
            _isSynchronizing = true;
            base.SyncRepair(peer, peerClock, missingObjects);
        }
        finally { _isSynchronizing = false; }

        // since we trust the peer for its FIFOness, we can close any gaps
        foreach(var n in peerClock.Keys.Intersect(_node.Context.GetPeerNodes(_node.Id)))
        {
            var set = peerClock[n];
            if (set.Bitmap.Any())
                _node.Logger.Warn("{Peer} sent FIFO node clock with gaps: {Set}", n, set);
            else
                NodeClock[n].UnionWith(set);
        }

        // apply or discard any cached operations
        CheckPending([peer]);
    }
}
