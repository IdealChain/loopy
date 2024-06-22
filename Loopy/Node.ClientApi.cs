using Loopy.Data;
using Loopy.Enums;
using NLog;

namespace Loopy;

public partial class Node
{
    public async Task<(Value[] values, CausalContext cc)> Get(Key k, int quorum = 1, ConsistencyMode mode = default)
    {
        using (ScopeContext.PushNestedState($"Get({k}, q{quorum})"))
        {
            // ensure "local" node is part of quorum, if it is among the replica nodes
            var replicaNodes = Context.GetReplicaNodes(k)
                .OrderByDescending(n => n == i)
                .Select(Context.GetNodeApi);

            var objs = new List<Data.Object>();
            var fetchTasks = replicaNodes.Take(quorum).Select(api => api.Fetch(k, mode)).ToList();

            while (objs.Count < quorum)
            {
                var finishedTask = await Task.WhenAny(fetchTasks);
                objs.Add(finishedTask.Result);
                fetchTasks.Remove(finishedTask);
            }

            // return merged result
            var m = objs.Aggregate(Merge);
            Logger.Trace("returning [{Merged} - {Mode}]", m, mode);
            return (m.DotValues.Values.ToArray(), m.CausalContext);
        }
    }

    public async Task Put(Key k, Value v, CausalContext? cc = default, ReplicationMode mode = default)
    {
        using (ScopeContext.PushNestedState($"Put({k}, {v})"))
        {
            var keyPriority = k.Priority;
            var replicaNodes = Context.GetReplicaNodes(k)
                .Where(n => n != i)
                .Select(Context.GetNodeApi);

            // generate a new version of this object
            var c = NodeClock[i].Max + 1;
            var o = new Data.Object();
            o.DotValues[(i, c)] = v;
            o.FifoDistances[(i, c)] = new FifoDistances(FifoPriorityPredecessor, c);
            o.CausalContext.MergeIn(cc ?? CausalContext.Initial);
            o.CausalContext[i] = c;

            // raise fifo predecessor for all lower-or-equal priorities
            for (var p = Priority.P0; p <= keyPriority; p++)
                FifoPriorityPredecessor[p] = c;

            // update and merge local object
            o = Update(k, o);

            Logger.Trace("created [{Merged}]", o);

            // forward the update to other key replicas
            async Task Replicate(string m)
            {
                try
                {
                    var replicaTasks = replicaNodes.Select(n => n.Update(k, o));
                    await Task.WhenAll(replicaTasks);
                    Logger.Trace("replicated [{Mode}]", m);
                }
                catch (OperationCanceledException e)
                {
                    Logger.Trace("replication canceled");
                }
            }

            if (mode == ReplicationMode.Sync)
                await Replicate("sync");
            else if (mode == ReplicationMode.Async)
                _ = Replicate("async").ContinueWith(t => { });
        }
    }

    public async Task Delete(Key k, CausalContext? cc = default, ReplicationMode mode = default)
    {
        await Put(k, Value.None, cc, mode);
    }
}
