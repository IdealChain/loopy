using Loopy.Data;
using Loopy.Enums;
using NLog;

namespace Loopy;

public partial class Node
{
    /// <summary>
    /// Fifo predecessor per prio (preceeding update id with equal-or-higher priority)
    /// </summary>
    private readonly int[] FifoPriorityPredecessor = new int[FifoExtensions.Priorities.Length];

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, int quorum = 1, ConsistencyMode mode = default,
        CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"Get({k}, q{quorum}, {mode})");

        // ensure "local" node is part of quorum, if it is among the replica nodes
        var replicaNodes = Context.GetReplicaNodes(k)
            .OrderByDescending(n => n == i)
            .Select(Context.GetNodeApi);

        var objs = new List<NdcObject>();
        var fetchTasks = replicaNodes.Take(quorum).Select(api => api.Fetch(k, mode, cancellationToken)).ToList();

        if (fetchTasks.Count < quorum)
            Logger.Warn("quorum cannot be met: {Count} nodes < {Quorum} quorum", fetchTasks.Count, quorum);

        while (objs.Count < quorum && fetchTasks.Count > 0)
        {
            var finishedTask = await Task.WhenAny(fetchTasks);
            objs.Add(finishedTask.Result);
            fetchTasks.Remove(finishedTask);
        }

        // return merged result
        var m = objs.Aggregate((o1, o2) => o1.Merge(o2));
        Logger.Trace("returning [{Merged}]", m);
        return (m.DotValues.Values.Select(v => v.value).ToArray(), m.CausalContext);
    }

    public Task Put(Key k, Value v, CausalContext? cc = default, NodeId[]? replicaFilter = default,
        CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"Put({k}, {v})");

        var keyPriority = k.Priority;
        var replicaNodes = new HashSet<NodeId>(Context.GetReplicaNodes(k).Where(n => n != i));
        if (replicaFilter != null)
            replicaNodes.IntersectWith(replicaFilter);

        // generate a new version of this object
        var c = EventualStore.GetClock()[i].Max + 1;
        var o = new NdcObject();
        var fifoDistances = FifoPriorityPredecessor.Select(pre => c - pre).ToArray();
        o.DotValues[(i, c)] = (v, fifoDistances);
        o.CausalContext.MergeIn(cc ?? CausalContext.Initial);
        o.CausalContext[i] = c;

        // raise fifo predecessor for all lower-or-equal priorities
        for (var p = Priority.P0; p <= keyPriority; p++)
            FifoPriorityPredecessor[(int)p] = c;

        // update and merge local object
        o = Update(k, o);
        Logger.Trace("created [{Merged}]", o);

        if (replicaNodes.Count == 0)
            return Task.CompletedTask;

        // forward the update to other key replicas
        foreach (var nodeApi in replicaNodes.Select(Context.GetNodeApi))
            nodeApi.SendUpdate(k, o);

        Logger.Trace("sent to: {ReplicaNodes}", string.Join(", ", replicaNodes));
        return Task.CompletedTask;

        // async Task ReplicateSync()
        // {
        //     try
        //     {
        //         await Task.WhenAll(replicaNodes.Select(n => Context.GetNodeApi(n).Update(k, o, cancellationToken)));
        //         Logger.Trace("replicated to: {ReplicaNodes}", string.Join(", ", replicaNodes));
        //     }
        //     catch (OperationCanceledException) { Logger.Trace("replication canceled"); }
        //     catch (Exception e) { Logger.Warn("replication failed: {Message}", e.Message); }
        // }
    }

    public async Task Delete(Key k, CausalContext? cc = default, NodeId[]? replicaFilter = default,
        CancellationToken cancellationToken = default)
    {
        await Put(k, Value.None, cc, replicaFilter, cancellationToken);
    }
}
