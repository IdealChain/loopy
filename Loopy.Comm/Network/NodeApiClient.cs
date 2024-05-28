using Loopy.Comm.Messages;
using Loopy.Data;
using Loopy.Enums;
using Loopy.Interfaces;
using NetMQ;
using NetMQ.Sockets;

namespace Loopy.Comm.Network
{
    public class NodeApiClient : IDisposable, IRemoteNodeApi
    {
        private readonly NetMQSocket _netMqSocket;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public NodeApiClient(string host = "localhost")
        {
            _netMqSocket = new RequestSocket($">tcp://{host}:{ClientApiServer.Port}");
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _netMqSocket.Dispose();
        }

        public async Task<Data.Object> Fetch(Key k, ConsistencyMode mode)
        {
            var req = new NodeFetchRequest { Key = k.Name, Mode = mode };
            var resp = await _netMqSocket.RemoteCall<NodeFetchRequest, NodeFetchResponse>(req, _cancellationTokenSource.Token);
            return resp.Obj;
        }

        public async Task<Data.Object> Update(Key k, Data.Object o)
        {
            var req = new NodeUpdateRequest { Key = k.Name, Obj = o };
            var resp = await _netMqSocket.RemoteCall<NodeUpdateRequest, NodeUpdateResponse>(req, _cancellationTokenSource.Token);
            return resp.Obj;
        }

        public async Task<(Map<NodeId, UpdateIdSet> NodeClock, List<(Key, Data.Object)> missingObjects)> SyncClock(NodeId p, Map<NodeId, UpdateIdSet> nodeClockP)
        {
            var req = new NodeSyncClockRequest
            {
                NodeId = p.Id,
                NodeClock = nodeClockP,
            };
            var resp = await _netMqSocket.RemoteCall<NodeSyncClockRequest, NodeSyncClockResponse>(req, _cancellationTokenSource.Token);
            return (resp.NodeClock, resp.MissingObjects.Select(kv => ((Key)kv.Key, (Data.Object)kv.Value)).ToList());
        }        
    }
}
