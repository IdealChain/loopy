using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NLog;
using Object = Loopy.Data.Object;

namespace Loopy.Consistency;

internal class FifoPriorityStore : IConsistencyStore
{
    private readonly Node _node;
    private readonly Priority _prio;
    private readonly Map<NodeId, UpdateIdSet> _fifoClock = new();
    private readonly Map<Key, Object> _fifoStorage = new();
    private readonly Map<NodeId, SortedList<int, (Key key, Object o)>> _pending = new();

    public FifoPriorityStore(Node node, Priority prio)
    {
        _node = node;
        _prio = prio;
    }

    public void CheckMerge(Key k, Object o)
    {
        // if the key's priority is below our level, it is filtered and must be ignored
        if (k.Priority < _prio)
            return;
        

        using (ScopeContext.PushNestedState($"MergeFifo({k}, {_prio})"))
        {
            // special handling for anti-entropy synced deletion: ignore empty object
            // (as the dot values are already received empty, we do not know node/update ID of the delete operation)
            if (o.DotValues.Count == 0)
            {
                _node.Logger.Trace("ignoring empty object [deletion]");
                return;
            }

            // group dots into immediately applicable (no gaps) and pending (gaps) sets, ignoring already applied ones
            var applicableDots = o.DotValues.Keys
                .Where(d => !_fifoClock[d.NodeId].Contains(d.UpdateId))
                .ToLookup(d => _fifoClock[d.NodeId].Base >= o.FifoDistances[d].GetPredecessorId(_prio, d.UpdateId));

            // short circuit the common case of no gaps: we can apply immediately, just as in the eventual store
            if (!applicableDots[false].Any())
            {
                Store(k, o);
                CheckPending(o.DotValues.Keys.Select(d => d.NodeId).Distinct());
                return;
            }

            // otherwise: immedatiely merge the applicable objects, schedule the rest for later
            if (applicableDots[true].Any())
                Store(k, SplitObject(o, applicableDots[true]));

            foreach (var dot in applicableDots[false])
            {
                if (_pending[dot.NodeId].ContainsKey(dot.UpdateId))
                    continue;

                _node.Logger.Trace("Pending [fifo gap]: {Dot}", dot);
                _pending[dot.NodeId].Add(dot.UpdateId, (k, SplitObject(o, new[] { dot })));
            }

            CheckPending(o.DotValues.Keys.Select(d => d.NodeId).Distinct());
        }
    }

    private Object SplitObject(Object o, IEnumerable<Dot> dots)
    {
        var s = new Object();
        s.DotValues.MergeIn(dots.Select(d => (d, o.DotValues[d])));
        s.FifoDistances.MergeIn(dots.Select(d => (d, o.FifoDistances[d])));
        s.CausalContext.MergeIn(o.CausalContext);
        return s;
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
                if (_fifoClock[d.NodeId].Base >= o.FifoDistances[d].GetPredecessorId(_prio, u))
                {
                    _node.Logger.Trace("Merging [no fifo gap]: {Dot}={Value}", d, o.DotValues[d]);
                    Store(k, o);
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

    public Object Fetch(Key k, Priority p = default)
    {
        if (!_fifoStorage.TryGetValue(k, out var obj) || obj.IsEmpty)
            obj = new Object();
        
        return _node.Fill(k, obj, _fifoClock);
    }

    private void Store(Key k, Object f)
    {
        var m = Node.Merge(_node.Fill(k, _fifoStorage[k], _fifoClock), f);
        var s = _node.Strip(m, _fifoClock);
        var (vers, cc, fd) = (s.DotValues, s.CausalContext, s.FifoDistances);

        if (s.IsEmpty && cc.Count == 0)
            _fifoStorage.Remove(k);
        else
            _fifoStorage[k] = s;

        foreach (var d in vers.Keys)
        {
            _fifoClock[d.NodeId].UnionWith(fd[d].GetSkippedUpdateIds(_prio, d.UpdateId));
            _fifoClock[d.NodeId].Add(d.UpdateId);
        }
    }

    public Map<NodeId, UpdateIdSet> GetClock() => _fifoClock;

    public (Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects) SyncClock(
        NodeId p, Map<NodeId, UpdateIdSet> pNodeClock)
    {
        using (ScopeContext.PushNestedState($"SyncFifoClock({p}, {_prio})"))
        {
            _node.Logger.Trace("syncing: {NC}", pNodeClock.ValuesToString());

            // get all keys from dots missing in the node p
            var missingKeys = new HashSet<Key>();
            foreach (var n in _node.Context.GetPeerNodes(_node.Id).Intersect(_node.Context.GetPeerNodes(p)))
            foreach (var c in _fifoClock[n].Except(pNodeClock[n]))
                missingKeys.Add(_node.DotKeyMap[(n, c)]);

            // get the missing objects from keys replicated by p
            var missingKeyObjects = new List<(Key, Object)>();
            foreach (var k in missingKeys)
            {
                if (_node.Context.GetReplicaNodes(k).Contains(p))
                    missingKeyObjects.Add((k, _fifoStorage[k]));
            }

            // remote sync_repair => return results instead of RPC
            _node.Logger.Trace("returning: {Missing} missing objects for {N}", missingKeyObjects.Count, p);
            return (_fifoClock, missingKeyObjects);
        }
    }

    public void SyncRepair(NodeId p, Map<NodeId, UpdateIdSet> pNodeClock, List<(Key, Object)> missingObjects)
    {
        using (ScopeContext.PushNestedState($"SyncFifoRepair({p}, {_prio})"))
        {
            _node.Logger.Trace("applying {NC}: {Missing} objects", pNodeClock.ValuesToString(), missingObjects.Count);

            // update local objects with the missing objects [trust that its FIFO]
            foreach (var (k, o) in missingObjects)
                Store(k, o);

            // merge p's node clock entry to close gaps
            _fifoClock[p].UnionWith(pNodeClock[p]);

            CheckPending([p]);
        }
    }
}
