using System.Collections;
using Loopy.Data;
using Loopy.Interfaces;
using Object = Loopy.Data.Object;

namespace Loopy.Test.LocalCluster;

public class LocalNodeCluster : IEnumerable<NodeId>, INodeContext
{
    private readonly Dictionary<NodeId, Node> _nodes;
    private CancellationTokenSource? _cancellationSource;
    private List<Task> _backgroundTasks = new();

    public LocalNodeCluster(int nodeCount)
    {
        _nodes = Enumerable.Range(1, nodeCount)
            .Select(i => new NodeId(i))
            .ToDictionary(n => n, n => new Node(n, this));
    }

    public void StartBackgroundTasks(double stripInterval = 30, double antiEntropyInterval = 30)
    {
        _ = StopBackgroundTasks();

        foreach (var node in _nodes.Values)
        {
            // start causality stripping and anti entropy background processes
            _backgroundTasks.Add(node.StripCausality(TimeSpan.FromSeconds(stripInterval), _cancellationSource!.Token));
            _backgroundTasks.Add(
                node.AntiEntropy(TimeSpan.FromSeconds(antiEntropyInterval), _cancellationSource!.Token));
        }
    }

    public Task StopBackgroundTasks()
    {
        _cancellationSource?.Cancel();
        _cancellationSource?.Dispose();
        _cancellationSource = new CancellationTokenSource();

        var pendingTasks = _backgroundTasks;
        _backgroundTasks = new();
        return Task.WhenAll(pendingTasks);
    }

    public async Task RunBackgroundTasksOnce()
    {
        StartBackgroundTasks();
        await StopBackgroundTasks();
    }

    public IEnumerable<NodeId> GetReplicaNodes(Key key) => _nodes.Keys;

    public IEnumerable<NodeId> GetPeerNodes(NodeId n) => _nodes.Keys;

    public IRemoteNodeApi GetNodeApi(NodeId id) => new RemoteNodeApiWrapper(_nodes[id]);

    public IClientApi GetClientApi(NodeId id) => _nodes[id];

    public IEnumerator<NodeId> GetEnumerator() => _nodes.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private class RemoteNodeApiWrapper : IRemoteNodeApi
    {
        private readonly Node _node;
        private readonly int _latency;

        public RemoteNodeApiWrapper(Node node, int latency = 10)
        {
            _node = node;
            _latency = latency;
        }

        public Task<Object> Fetch(Key k, ConsistencyMode mode)
        {
            // Thread.Sleep(_latency / 10);

            switch (mode)
            {
                case ConsistencyMode.Eventual: return Task.FromResult(_node.Fetch(k));
                case ConsistencyMode.Fifo: return Task.FromResult(_node.FetchFifo(k));
                case ConsistencyMode.Causal: return Task.FromResult(_node.FetchCausal(k));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public Task<Object> Update(Key k, Object o)
        {
            // Thread.Sleep(_latency);
            return Task.FromResult(_node.Update(k, o));
        }

        public Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Object)> missingObjects)> SyncClock(
            NodeId p, Map<NodeId, UpdateIdSet> nodeClockP)
        {
            // Thread.Sleep(_latency);
            return Task.FromResult(_node.SyncClock(p, nodeClockP));
        }
    }
}