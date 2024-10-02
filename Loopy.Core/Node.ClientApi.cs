using Loopy.Core.Data;
using Loopy.Core.Enums;
using NLog;

namespace Loopy.Core;

internal partial class Node
{
    /// <summary>
    /// Quorum fetching timeout to ask the next available replica
    /// </summary>
    private static readonly TimeSpan QuorumQueryNextNodeInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Whether to wait for replication acknowledgment responses
    /// </summary>
    private static readonly bool SynchronousReplication = false;

    /// <summary>
    /// Fifo predecessor per prio (preceeding update id with equal-or-higher priority)
    /// </summary>
    private int[] FifoPriorityPredecessor { get; } = new int[FifoExtensions.Priorities.Length];

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, int quorum = 1, ConsistencyMode mode = default,
        CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"Get({k}, q{quorum}, {mode})");

        // short circuit common case: directly fetch from local node
        NdcObject obj;
        if (quorum <= 1 && Context.ReplicationStrategy.GetReplicaNodes(k).Contains(Id))
            obj = await Fetch(k, mode, cancellationToken);
        else
            obj = await GetFromReplicaQuorum(k, quorum, mode, cancellationToken);

        Logger.Debug("returning {Obj}", obj);
        return (obj.DotValues.GetDistinctValues(), obj.CausalContext);
    }

    private async Task<NdcObject> GetFromReplicaQuorum(Key k, int quorum, ConsistencyMode mode, CancellationToken cancellationToken)
    {
        // ensure "local" node is part of quorum, if it is among the replica nodes
        var remainingNodes = new Queue<NodeId>(Context.ReplicationStrategy.GetReplicaNodes(k).OrderByDescending(n => n == Id));
        if (remainingNodes.Count < quorum)
        {
            Logger.Warn("reducing requested quorum {Quorum} to {Remaining} available nodes", quorum, remainingNodes.Count);
            quorum = remainingNodes.Count;
        }

        var objs = new List<NdcObject>(quorum);
        var pendingQueries = new Dictionary<Task, NodeId>(quorum);

        // send initial queries
        while (pendingQueries.Count < quorum && remainingNodes.Count > 0)
        {
            var node = remainingNodes.Dequeue();
            pendingQueries.Add(Context.GetNodeApi(node).Fetch(k, mode, cancellationToken), node);
        }
        Logger.Debug("initially querying {Nodes}...", pendingQueries.Values.AsCsv());

        while (objs.Count < quorum && !cancellationToken.IsCancellationRequested)
        {
            if (pendingQueries.Count == 0)
            {
                Logger.Warn("quorum not met, but out of replica nodes to query");
                throw new TaskCanceledException("quorum not met");
            }

            // wait for next result or timeout
            var nextReplicaTimeout = Task.Delay(QuorumQueryNextNodeInterval, cancellationToken);
            var completeTask = await Task.WhenAny(pendingQueries.Keys.Append(nextReplicaTimeout));

            if (completeTask is Task<NdcObject> fetchTask &&
                pendingQueries.Remove(fetchTask, out var node) &&
                fetchTask.IsCompletedSuccessfully)
            {
                objs.Add(await fetchTask);
                Logger.Debug("got {Node} response [{Count}/{Quorum}]", node, objs.Count, quorum);
            }
            else if (remainingNodes.Count > 0 && mode == ConsistencyMode.Eventual)
            {
                // query an additional replica node
                node = remainingNodes.Dequeue();
                pendingQueries.Add(Context.GetNodeApi(node).Fetch(k, mode, cancellationToken), node);
                Logger.Debug("additionally querying {Node}...", node);
            }
        }

        // return merged result
        cancellationToken.ThrowIfCancellationRequested();
        return objs.Aggregate((o1, o2) => o1.Merge(o2));
    }

    public async Task Put(Key k, Value v, CausalContext? cc = default, NodeId[]? replicaFilter = default,
        CancellationToken cancellationToken = default)
    {
        using var _ = ScopeContext.PushNestedState($"Put({k}, {v})");

        var keyPriority = k.Priority;
        var replicaNodes = new HashSet<NodeId>(Context.ReplicationStrategy.GetReplicaNodes(k).Where(n => n != Id));
        if (replicaFilter != null)
            replicaNodes.IntersectWith(replicaFilter);

        var o = new NdcObject();
        using (await StoreLock.EnterWriteAsync(cancellationToken))
        {
            // generate a new version of this object
            var dot = EventualStore.GetNextVersion();
            var fifoDistances = FifoPriorityPredecessor.Select(pre => dot.UpdateId - pre).ToArray();
            o.DotValues[dot] = (v, fifoDistances);
            o.CausalContext.MergeIn(cc ?? CausalContext.Initial);
            o.CausalContext[Id] = dot.UpdateId;

            // raise fifo predecessor for all lower-or-equal priorities
            for (var p = Priority.P0; p <= keyPriority; p++)
                FifoPriorityPredecessor[(int)p] = dot.UpdateId;

            // update and merge local object
            o = UpdateUnderLock(k, o);
        }

        // forward the update to other key replicas
        if (replicaNodes.Count > 0)
        {
            if (!SynchronousReplication)
                await Replicate(replicaNodes, k, o, cancellationToken);
            else
                await ReplicateWithAck(replicaNodes, k, o, cancellationToken);
        }
    }

    /// <summary>
    /// Async replication: fire and forget
    /// </summary>
    private async Task Replicate(IEnumerable<NodeId> replicaNodes, Key k, NdcObject o, CancellationToken ct)
    {
        Logger.Debug("replicating async to {Nodes}", replicaNodes.AsCsv());
        await Task.WhenAll(replicaNodes.Select(n => Context.GetNodeApi(n).SendUpdate(k, o, ct)));
        return;
    }

    /// <summary>
    /// Synchronous replication: wait for ACK confirmations
    /// </summary>
    private async Task ReplicateWithAck(IEnumerable<NodeId> replicaNodes, Key k, NdcObject o, CancellationToken ct)
    {
        Logger.Debug("replicating sync to {Nodes}", replicaNodes.AsCsv());
        var pendingTasks = replicaNodes.ToDictionary(id => Context.GetNodeApi(id).Update(k, o, ct));
        while (pendingTasks.Count > 0 && !ct.IsCancellationRequested)
        {
            var done = await Task.WhenAny(pendingTasks.Keys);
            if (done.IsCompletedSuccessfully && pendingTasks.Remove(done, out var node))
                Logger.Debug("got {Node} replication ack", node);
        }
    }

    public async Task Delete(Key k, CausalContext? cc = default, NodeId[]? replicaFilter = default,
        CancellationToken cancellationToken = default)
    {
        await Put(k, Value.None, cc, replicaFilter, cancellationToken);
    }
}
