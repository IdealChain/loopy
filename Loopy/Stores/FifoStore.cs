using Loopy.Data;
using Loopy.Enums;
using NLog;

namespace Loopy.Stores;

internal class FifoStore : NdcStoreBase
{
    private readonly Node _node;
    private readonly Priority _minPrio;

    /// <summary>
    /// Per-node limit of not-yet-visible updates to be kept in buffer
    /// </summary>
    private const int BufferedUpdatesLimit = 1000;

    /// <summary>
    /// Storage for a sorted set of contiguous segments of not-yet-applied object updates -
    /// note that while these changes are all done by the same node, they might affect different keys
    /// </summary>
    private readonly Dictionary<NodeId, FifoSegmentSet<Dictionary<Key, NdcObject>>> _bufferedSegments = new();

    public FifoStore(Node node, Priority minPrio) : base(node.Id, node.Context)
    {
        _node = node;
        _minPrio = minPrio;
    }

    public void ProcessUpdate(Key k, NdcObject o)
    {
        using var _ = ScopeContext.PushNestedState($"Fifo({k}, {_minPrio})");

        // if the key's priority is below our level, it is filtered and must be ignored
        if (k.Priority < _minPrio)
            return;

        // short circuit the common case of no gaps: we can apply immediately, just as in the eventual store
        if (o.DotValues.All(kv =>
                CanMerge(kv.Key.NodeId, new UpdateIdRange(kv.Key.UpdateId, _minPrio, kv.Value.fifoDistances))))
            Update(k, o);
        else
            Buffer(k, o);

        CheckBufferedSegments((o.DotValues.Keys.Select(d => d.NodeId)));
    }

    private void Buffer(Key k, NdcObject o)
    {
        foreach (var (dot, value) in o.DotValues)
        {
            if (dot.UpdateId - NodeClock[dot.NodeId].Base > BufferedUpdatesLimit)
            {
                _node.Logger.Warn("dropping (buffer size limit exceeded): {Dot}", dot);
                continue;
            }

            var dotObject = o.DotValues.Count > 1 ? o.Split([dot]) : o;
            var dotRange = new UpdateIdRange(dot.UpdateId, _minPrio, value.fifoDistances);

            if (CanMerge(dot.NodeId, dotRange))
                Update(k, dotObject);
            else
                BufferSegment(dot.NodeId, dotRange, k, dotObject);
        }
    }

    private void BufferSegment(NodeId node, UpdateIdRange range, Key k, NdcObject dotObject)
    {
        if (!_bufferedSegments.TryGetValue(node, out var nodeBuffer))
            _bufferedSegments[node] = nodeBuffer = new(MergeSegments);

        _node.Logger.Trace("buffering gapped segment: {Node} {Range}", node, range);
        var segment = new Dictionary<Key, NdcObject> { { k, dotObject } };
        nodeBuffer.Add(range, segment);
    }

    /// <summary>
    /// Merge operation that combines the content of two segments when their ranges are merged
    /// </summary>
    private static Dictionary<Key, NdcObject> MergeSegments(Dictionary<Key, NdcObject> seg1,
        Dictionary<Key, NdcObject> seg2)
    {
        seg1.MergeIn(seg2, (o1, o2) => o1.Merge(o2));
        return seg1;
    }

    /// <summary>
    /// Checks whether the FIFO condition is satisfied:
    /// no gap between the node's base clock and the lower range boundary
    /// </summary>
    private bool CanMerge(NodeId node, UpdateIdRange range) => range.First <= NodeClock[node].Base + 1;

    /// <summary>
    /// Check the buffered segments and merge all that have no FIFO gap anymore
    /// </summary>
    private void CheckBufferedSegments(IEnumerable<NodeId> nodes)
    {
        foreach (var n in nodes)
        {
            if (!_bufferedSegments.TryGetValue(n, out var segments))
                continue;

            // pop and apply all segments that have no gap left
            while (segments.Count > 0 && CanMerge(n, segments.PeekRange))
            {
                var (range, objects) = segments.Pop();
                _node.Logger.Trace("merging buffered segment: {Node} {Range}", n, range);
                foreach (var (k, o) in objects)
                    Update(k, o);
            }

            if (segments.Count == 0)
                _bufferedSegments.Remove(n);

            if (NodeClock[n].Bitmap.Any())
                _node.Logger.Warn("FIFO condition violated: gaps for {Peer} {Updates}", n, NodeClock[n]);
        }
    }

    protected override void Store(Key k, NdcObject o)
    {
        // fill in update IDs that are allowed to be skipped
        foreach (var dv in o.DotValues)
            NodeClock[dv.Key.NodeId].UnionWith(dv.GetFifoSkippableUpdates(_minPrio));

        base.Store(k, o);
    }

    public override ModeSyncResponse SyncClock(NodeId peer, ModeSyncRequest request)
    {
        var response = base.SyncClock(peer, request);

        // augment missing objects with missing FIFO segments
        foreach (var (n, ns) in _bufferedSegments)
            foreach (var s in ns.Where(s => s.range.Last > request.PeerClock[n].Base))
                response.BufferedSegments.Add((n, s.range, s.value.Select(kv => (kv.Key, kv.Value)).ToList()));

        return response;
    }

    public override void SyncRepair(NodeId peer, ModeSyncResponse response)
    {
        // update local objects with the missing FIFO objects
        base.SyncRepair(peer, response);

        // since we trust the peer for its FIFOness, we can close any gaps
        var peerClock = response.PeerClock;
        var peerNodes = new HashSet<NodeId>(_node.Context.GetPeerNodes(_node.Id));
        foreach (var (n, c) in peerClock)
        {
            if (!peerNodes.Contains(n))
                continue;

            if (!c.Bitmap.Any())
                NodeClock[n].UnionWith(c);
            else
                _node.Logger.Warn("{Peer} sent FIFO node clock with gaps: {Set}", n, c);
        }

        // add received segments to buffer
        foreach (var (node, range, segment) in response.BufferedSegments)
        {
            if (!peerNodes.Contains(node))
                continue;

            foreach (var (key, dotObject) in segment)
                BufferSegment(node, range, key, dotObject);
        }

        // apply any buffered operations
        CheckBufferedSegments(peerNodes);
    }
}
