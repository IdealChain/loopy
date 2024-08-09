using Loopy.Core.Data;
using Loopy.Core.Enums;
using NLog;

namespace Loopy.Core;

public partial class Node
{
    /// <summary>
    /// Fifo predecessor per prio (preceeding update id with equal-or-higher priority)
    /// </summary>
    private int[] FifoPriorityPredecessor { get; } = new int[FifoExtensions.Priorities.Length];

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, int quorum = 1, ConsistencyMode mode = default,
        CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"Get({k}, q{quorum}, {mode})");

        // ensure "local" node is part of quorum, if it is among the replica nodes
        var replicaNodes = Context.GetReplicaNodes(k)
            .OrderByDescending(n => n == Id)
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
        var replicaNodes = new HashSet<NodeId>(Context.GetReplicaNodes(k).Where(n => n != Id));
        if (replicaFilter != null)
            replicaNodes.IntersectWith(replicaFilter);

        // generate a new version of this object
        var dot = EventualStore.GetNextVersion();
        var o = new NdcObject();
        var fifoDistances = FifoPriorityPredecessor.Select(pre => dot.UpdateId - pre).ToArray();
        o.DotValues[dot] = (v, fifoDistances);
        o.CausalContext.MergeIn(cc ?? CausalContext.Initial);
        o.CausalContext[Id] = dot.UpdateId;

        // raise fifo predecessor for all lower-or-equal priorities
        for (var p = Priority.P0; p <= keyPriority; p++)
            FifoPriorityPredecessor[(int)p] = dot.UpdateId;

        // update and merge local object
        o = Update(k, o);

        // forward the update to other key replicas
        if (replicaNodes.Count > 0)
        {
            foreach (var nodeApi in replicaNodes.Select(Context.GetNodeApi))
                nodeApi.SendUpdate(k, o);

            Logger.Trace("sent to {ReplicaNodes}: {Object}", string.Join(", ", replicaNodes), o);
        }

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
