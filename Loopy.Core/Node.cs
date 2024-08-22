using Loopy.Core.Data;
using Loopy.Core.Enums;
using Loopy.Core.Interfaces;
using Loopy.Core.Stores;
using NLog;
using System.Diagnostics;

namespace Loopy.Core;

/// <summary>
/// Prototype node for the Loopy distributed data store
/// </summary>
[DebuggerDisplay("Node {Id.Id}")]
internal partial class Node
{
    public INodeContext Context { get; }
    public NodeId Id => Context.NodeId;
    public ILogger Logger => Context.Logger;

    public Node(INodeContext context)
    {
        Context = context;
        Stores[ConsistencyMode.Eventual] = EventualStore = new EventualStore(Context);
        Stores[ConsistencyMode.Fifo] = new FifoStore(Context, Priority.P0);
        Stores[ConsistencyMode.FifoP1] = new FifoStore(Context, Priority.P1);
        Stores[ConsistencyMode.FifoP2] = new FifoStore(Context, Priority.P2);
        Stores[ConsistencyMode.FifoP3] = new FifoStore(Context, Priority.P3);
    }

    /// <summary>
    /// Lock to limit concurrent access to the node stores' data
    /// </summary>
    private NodeLock StoreLock { get; } = new();

    private EventualStore EventualStore { get; }

    private Dictionary<ConsistencyMode, INdcStore> Stores { get; } = new();

    public async Task<NdcObject> Fetch(Key k, ConsistencyMode mode, CancellationToken ct)
    {
        using var _ = ScopeContext.PushNestedState($"Fetch({k}, {mode})");
        using (await StoreLock.EnterReadAsync(ct))
            return FetchUnderLock(k, mode);
    }

    private NdcObject FetchUnderLock(Key k, ConsistencyMode mode)
    {
        var o = Stores[mode].Fetch(k);
        Logger.Debug("fetched [{Obj}]", o);
        return o;
    }

    public async Task<NdcObject> Update(Key k, NdcObject o, CancellationToken ct)
    {
        using var _ = ScopeContext.PushNestedState($"Update({k})");
        using (await StoreLock.EnterWriteAsync(ct))
            return UpdateUnderLock(k, o);
    }

    private NdcObject UpdateUnderLock(Key k, NdcObject o)
    {
        // update the eventual store and allow all the FIFO stores to apply or cache any updates
        var m = EventualStore.Update(k, o);
        foreach (var cs in Stores.Values.OfType<FifoStore>())
            cs.ProcessUpdate(k, m);

        return m;
    }
}
