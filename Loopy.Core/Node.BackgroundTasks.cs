using Loopy.Core.Data;
using Loopy.Core.Enums;
using NLog;
using System.Diagnostics;

namespace Loopy.Core;

internal partial class Node
{
    public async Task StripCausality(CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState("StripCausality");

        using (await StoreLock.EnterWriteAsync(cancellationToken))
            foreach (var (_, s) in Stores)
                s.StripCausality();
    }

    public async Task AntiEntropy(NodeId peer, CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"AntiEntropy({Id}<-{peer})");
        Debug.Assert(peer != Id);
        Logger.Debug("requesting sync from {Peer}", peer);

        // 1. gather local clocks
        var request = new SyncRequest { Peer = Id };
        using (await StoreLock.EnterReadAsync(cancellationToken))
        {
            foreach (var (m, s) in Stores)
                request[m] = s.GetSyncRequest();
        }

        // 2. send local clocks to peer for comparison (the lock can be released until we receive the results)
        var response = await Context.GetNodeApi(peer).SyncClock(request, cancellationToken);
        Debug.Assert(response.Peer == peer);
        Logger.Debug("got {Objs} missing objects, {Segs} buffered segments",
            response.Values.Sum(v => v.MissingObjects.Count), response.Values.Sum(v => v.BufferedSegments.Count));

        // 3. merge received peer clocks and missing objects
        using (await StoreLock.EnterWriteAsync(cancellationToken))
        {
            foreach (var (m, s) in Stores)
                s.SyncRepair(response.Peer, response[m]);
        }
    }

    internal async Task<SyncResponse> SyncClock(SyncRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"SyncClock({Id}->{request.Peer})");

        // compare received peer clocks and return any missing objects
        var response = new SyncResponse { Peer = Id };

        using (await StoreLock.EnterReadAsync(cancellationToken))
        {
            foreach (var (m, s) in Stores)
            {
                response[m] = s.SyncClock(request.Peer, request[m]);

                if (response[m].MissingObjects.Count > 0)
                    Logger.Debug("{Mode}: returning {Objs} objects", m, response[m].MissingObjects.Count);

                if (response[m].BufferedSegments.Count > 0)
                    Logger.Debug("{Mode}: returning {Segs} buffered segments", m, response[m].BufferedSegments.Count);
            }
        }

        // remote sync_repair => return results instead of RPC
        return response;
    }

    public async Task Heartbeat(TimeSpan tolerance, CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"Heartbeat()");
        var now = DateTimeOffset.Now;

        // update our own timestamp
        await Put(Id.ToString(), now.ToString(), cancellationToken: cancellationToken);

        // evaluate peer timestamps
        foreach (var n in Context.ReplicationStrategy.GetPeerNodes(Id).Where(n => n != Id))
        {
            var ages = await Task.WhenAll(GetAge(n, ConsistencyMode.Fifo), GetAge(n, ConsistencyMode.Eventual));
            var (fifoAge, evAge) = (ages[0], ages[1]);
            if (!fifoAge.HasValue || !evAge.HasValue)
                continue;

            if (fifoAge.Value > tolerance && evAge.Value <= tolerance)
                Logger.Warn("{Node}: up, but missing FIFO values for {Age}", n, fifoAge.Value);
            else if (evAge.Value > tolerance)
                Logger.Warn("{Node}: not heard from for {Age}", n, evAge.Value);
        }

        async Task<TimeSpan?> GetAge(NodeId node, ConsistencyMode mode)
        {
            var (values, _) = await Get(node.ToString(), 1, mode, cancellationToken);

            if (values.Length == 0)
            {
                Logger.Debug("no heartbeat yet from {Node}", node);
                return null;
            }

            if (values.Length > 1 || !DateTimeOffset.TryParse(values[0].Data, out var last))
            {
                Logger.Warn("invalid heartbeat timestamp from {Node} ({Values})", node, values.AsCsv());
                return null;
            }

            return now - last;
        }
    }
}
