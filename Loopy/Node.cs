using System.Diagnostics;
using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using Loopy.Stores;
using NLog;

namespace Loopy;

/// <summary>
/// Prototype node for the Loopy distributed data store
/// </summary>
[DebuggerDisplay("Node {Id.Id}")]
public partial class Node
{
    private NodeId i;

    public NodeId Id => i;
    public Logger Logger { get; }
    public INodeContext Context { get; }

    public Node(NodeId id, INodeContext context)
    {
        i = id;
        Logger = LogManager.GetLogger($"{nameof(Node)}({Id.Id:00})");
        Context = context;

        Stores[ConsistencyMode.Eventual] = EventualStore = new EventualStore(this);

        Stores[ConsistencyMode.Fifo] = new FifoStore(this, Priority.P0);
        Stores[ConsistencyMode.FifoP1] = new FifoStore(this, Priority.P1);
        Stores[ConsistencyMode.FifoP2] = new FifoStore(this, Priority.P2);
        Stores[ConsistencyMode.FifoP3] = new FifoStore(this, Priority.P3);
    }

    /// <summary>
    /// Lock to limit concurrent access to node's data
    /// </summary>
    public readonly AwaitableLock NodeLock = new();

    private EventualStore EventualStore { get; }

    private Dictionary<ConsistencyMode, INdcStore> Stores { get; } = new();

    public NdcObject Fetch(Key k, ConsistencyMode mode = ConsistencyMode.Eventual)
    {
        using var _ = ScopeContext.PushNestedState($"Fetch({k}, {mode})");

        var o = Stores[mode].Fetch(k);
        Logger.Trace("fetched [{Fetched}]", o);
        return o;
    }

    public NdcObject Update(Key k, NdcObject o)
    {
        using var _ = ScopeContext.PushNestedState($"Update({k})");

        var m = EventualStore.Update(k, o);
        Logger.Trace("stored [{Stored}]", m);

        // afterwards, allow all the FIFO stores to apply or cache any updates
        foreach (var cs in Stores.Values.OfType<FifoStore>())
            cs.ProcessUpdate(k, m);

        return m;
    }
}
