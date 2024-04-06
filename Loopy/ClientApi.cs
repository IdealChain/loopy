using NLog;

namespace Loopy;

/// <summary>
/// Client API at Node
/// </summary>
public interface IClientApi
{
    /// <summary>
    /// Fetches the given key from quorum replicas and
    /// returns all concurrent values along with the causal context 
    /// </summary>
    Task<(Value[] values, CausalContext cc)> Get(Key k, int quorum = 1);

    /// <summary>
    ///  Forms a new version of the object identified by the given key,
    ///  sending the object to other nodes that replicate that key
    /// </summary>
    Task Put(Key k, Value v, CausalContext cc);

    /// <summary>
    /// Forms a new, deleted version of the object,
    /// sending the empty value to other nodes that replicate that key
    /// </summary>
    Task Delete(Key k, CausalContext cc);
}

public class ClientApi : IClientApi
{
    public ILogger Logger;

    private Node Node { get; }

    public ClientApi(Node node)
    {
        Node = node;
        Logger = LogManager.GetCurrentClassLogger().WithProperty("node", node.i);
    }

    public async Task<(Value[] values, CausalContext cc)> Get(Key k, int quorum = 1)
    {
        var localNode = Node.Context.GetNodeApi(Node.i);
        var replicaNodes = Node.Context.GetReplicaNodes(k)
            .Where(n => n != Node.i)
            .Select(Node.Context.GetNodeApi);

        // remote fetch from quorum nodes (ensure "local" node is within quorum)
        var objs = new List<Object>();
        objs.Add(await localNode.Fetch(k));

        if (quorum > 1)
        {
            var fetchTasks = replicaNodes.Select(api => api.Fetch(k)).ToList();

            while (objs.Count < quorum)
            {
                var finishedTask = await Task.WhenAny(fetchTasks);
                objs.Add(finishedTask.Result);
                fetchTasks.Remove(finishedTask);
            }
        }

        // return merged result
        var m = objs.Aggregate(Node.Merge);
        Logger.Trace("Get({Key}) => {Merged}", k, m);
        return (m.vers.Values.ToArray(), cc: new CausalContext(m.cc));
    }

    public async Task Put(Key k, Value v, CausalContext cc)
    {
        var replicaNodes = Node.Context.GetReplicaNodes(k)
            .Where(n => n != Node.i)
            .Select(Node.Context.GetNodeApi);

        // generate a new version of this object
        var i = Node.i;
        var c = Node.NodeClock[i].Max + 1;
        var o = new Object();
        o.vers[(i, c)] = v;
        o.cc.MergeIn(cc.cc);
        o.cc[i] = c;

        // update and merge local object
        o = Node.Update(k, o);
        Logger.Trace("Put({Key}, {Value}) => {Merged}", k, v, o);

        // forward the update to other key replicas
        var updateTasks = replicaNodes.Select(n => n.Update(k, o)).ToList();
        await Task.WhenAll(updateTasks);
    }

    public async Task Delete(Key k, CausalContext cc) => await Put(k, Value.Null, cc);
}