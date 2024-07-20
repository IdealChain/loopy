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
        var request = new SyncRequest { Peer = i };
        foreach (var (m, s) in Stores)
            request[m] = s.GetClock();

        // 2. send local clocks to peer for comparison
        var response = await Context.GetNodeApi(peer).SyncClock(request, cancellationToken);
        Trace.Assert(response.Peer == peer);

        // 3. merge received peer clocks and missing objects
        foreach (var (m, s) in Stores)
            s.SyncRepair(response.Peer, response[m].clock, response[m].missingObjects);
    }

    internal SyncResponse SyncClock(SyncRequest request)
    {
        using var _ = ScopeContext.PushNestedState($"SyncClock({i}->{request.Peer})");
         
        // compare received peer clocks and return any missing objects
        var response = new SyncResponse { Peer = i };
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

        var ts = DateTimeOffset.Now;
        foreach(var p in FifoExtensions.Priorities)
            await Put($"{p}_{Id}", ts.ToString(), cancellationToken: cancellationToken);

        foreach(var n in Context.GetPeerNodes(Id).Where(n => n != Id))
        {
            var result = await Get(n.ToString(), mode: Enums.ConsistencyMode.Fifo, cancellationToken: cancellationToken);
            if (result.values.Length == 1 &&
                DateTimeOffset.TryParse(result.values[0].Data, out var last) &&
                ts - last > tolerance)
            {
                Logger.Warn("not heard from {Node} for {Age}", n, ts - last);
            }
        }
    }
}
