using Loopy.Data;
using Loopy.Enums;
using NLog;
using System.Diagnostics;

namespace Loopy;

public partial class Node
{
    public Task StripCausality()
    {
        using var _ = ScopeContext.PushNestedState("StripCausality");
        foreach (var (_, s) in Stores)
            s.StripCausality();

        return Task.CompletedTask;
    }

    public async Task AntiEntropy(NodeId peer, CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"AntiEntropy({Id}<-{peer})");
        Trace.Assert(peer != Id);
        Logger.Trace("requesting sync from {Peer}", peer);

        // 1. gather local clocks
        var request = new SyncRequest { Peer = Id };
        foreach (var (m, s) in Stores)
            request[m] = s.GetClock();

        // 2. send local clocks to peer for comparison
        var response = await Context.GetNodeApi(peer).SyncClock(request, cancellationToken);
        Trace.Assert(response.Peer == peer);
        Logger.Trace("got {Missing} missing objects", response.Values.Sum(v => v.missingObjects.Count));

        // 3. merge received peer clocks and missing objects
        foreach (var (m, s) in Stores)
            s.SyncRepair(response.Peer, response[m].clock, response[m].missingObjects);
    }

    internal SyncResponse SyncClock(SyncRequest request)
    {
        using var _ = ScopeContext.PushNestedState($"SyncClock({Id}->{request.Peer})");

        // compare received peer clocks and return any missing objects
        var response = new SyncResponse { Peer = Id };
        foreach (var (m, s) in Stores)
        {
            var (_, missingObjects) = response[m] = s.SyncClock(request.Peer, request[m]);

            if (missingObjects.Count > 0)
                Logger.Trace("{Mode}: returning {Missing} objects", m, missingObjects.Count);
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
        foreach (var n in Context.GetPeerNodes(Id).Where(n => n != Id))
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
            if (values.Length == 1 && DateTimeOffset.TryParse(values[0].Data, out var last))
                return now - last;

            Logger.Warn("no valid heartbeat timestamp from {Node} ({Values})", node, values.AsCsv());
            return null;
        }
    }
}
