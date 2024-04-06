using System.Collections;

namespace Loopy;

public class NodeContext : IEnumerable<NodeId>
{
    private readonly Dictionary<NodeId, Node> _nodes;
    private CancellationTokenSource _cancellationSource = new CancellationTokenSource();

    public NodeContext(int nodeCount, bool enableBackgroundProcesses = false)
    {
        _nodes = Enumerable.Range(1, nodeCount)
            .Select(i => new NodeId(i))
            .ToDictionary(n => n, n => new Node(n, this));

        EnableBackgroundProcesses = enableBackgroundProcesses;
    }

    public bool EnableBackgroundProcesses
    {
        set
        {
            _cancellationSource.Cancel();
            _cancellationSource.Dispose();
            _cancellationSource = new CancellationTokenSource();

            if (!value)
                return;
            
            foreach (var node in _nodes.Values)
            {
                // start causality stripping and anti entropy background processes
                _ = node.StripCausality(TimeSpan.FromSeconds(15), _cancellationSource.Token);
                _ = node.AntiEntropy(TimeSpan.FromSeconds(30), _cancellationSource.Token);
            }
        }
    }

    /// <summary>
    /// Gets all nodes that should be replicas for a given key
    /// </summary>
    public IEnumerable<NodeId> GetReplicaNodes(Key key) => _nodes.Keys;

    /// <summary>
    /// Gets the peer nodes for the given node, i.e., all nodes that share
    /// replicas of some objects (including the node itself)
    /// </summary>
    public IEnumerable<NodeId> GetPeerNodes(NodeId n) => _nodes.Keys;

    /// <summary>
    /// Gets the node-to-node RPC API 
    /// </summary>
    public INodeApi GetNodeApi(NodeId id) => new NodeApi(_nodes[id]);

    /// <summary>
    /// Gets the client facing RPC API 
    /// </summary>
    public IClientApi GetClientApi(NodeId id) => new ClientApi(_nodes[id]);

    public Node GetNode(NodeId id) => _nodes[id];

    public IEnumerator<NodeId> GetEnumerator() => _nodes.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}