using Loopy.Core.Data;
using Loopy.Core.Interfaces;
using NLog;

namespace Loopy.Core.Api;

public class NodeContext : INodeContext
{
    private readonly Node _node;
    private readonly Func<NodeId, INodeApi> _remoteNodeApiFactory;
    private readonly Dictionary<NodeId, INodeApi> _nodeApiCache = new();

    public NodeContext(NodeId nodeId, IReplicationStrategy replicationStrategy, Func<NodeId, INodeApi> remoteNodeApiFactory)
    {
        NodeId = nodeId;
        Logger = LogManager.GetLogger(nodeId.ToString());
        ReplicationStrategy = replicationStrategy;

        _node = new Node(this);
        BackgroundTasks = new BackgroundTaskScheduler(_node);

        // prepopulate node-to-self loopback API
        _remoteNodeApiFactory = remoteNodeApiFactory;
        _nodeApiCache[nodeId] = new LocalNodeApi(_node);
    }

    public BackgroundTaskScheduler BackgroundTasks { get; }

    public NodeId NodeId { get; }
    public ILogger Logger { get; }
    public IReplicationStrategy ReplicationStrategy { get; }

    public IClientApi GetClientApi() => new LocalClientApi(_node);
    public IClientApi GetClientApi(NodeId[]? replicationFilter) => new LocalClientApi(_node) { ReplicationFilter = replicationFilter };
    public INodeApi GetNodeApi() => new LocalNodeApi(_node);

    INodeApi INodeContext.GetNodeApi(NodeId id)
    {
        if (!_nodeApiCache.TryGetValue(id, out var nodeApi))
            _nodeApiCache[id] = nodeApi = _remoteNodeApiFactory(id);

        return nodeApi;
    }
}
