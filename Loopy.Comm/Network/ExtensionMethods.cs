using Loopy.Comm.Messages;
using NetMQ;
using NetMQ.Sockets;

namespace Loopy.Comm.Network
{
    internal static class ExtensionMethods
    {
        public static async Task<(IMessage msg, NetMQMessage? header)> ReceiveMessage(this NetMQSocket socket, CancellationToken token)
        {
            var mq = await socket.ReceiveMultipartMessageAsync(cancellationToken: token);

            // starting from the end, find the empty frame delimiter. if there is one, build the header
            int? msgStart = null;
            NetMQMessage? header = null;
            for (var i = mq.FrameCount - 2; i >= 0; i--)
            {
                if (!msgStart.HasValue && mq[i].IsEmpty)
                {
                    msgStart = i + 1;
                    header = new();
                }
                else if (msgStart.HasValue && header != null)
                {
                    header.Push(mq[i]);
                }
            }

            var msg = MessageSerializer.Deserialize(mq, msgStart.GetValueOrDefault());
            return (msg, header);
        }

        public static bool SendMessage<TMsg>(this NetMQSocket socket, TMsg msg, NetMQMessage? header = null)
            where TMsg : IMessage
        {
            var mq = new NetMQMessage(header ?? Enumerable.Empty<NetMQFrame>());

            // append empty frame only if a header was given (request socket prepends its own envelope)
            if (header != null)
                mq.AppendEmptyFrame();

            MessageSerializer.Serialize(mq, msg);
            return socket.TrySendMultipartMessage(mq);
        }

        public static async Task<TResp> RemoteCall<TReq, TResp>(this RequestSocket socket, TReq msg, CancellationToken token)
            where TReq : IMessage
            where TResp : IMessage
        {
            socket.SendMessage(msg);
            var (resp, _) = await socket.ReceiveMessage(token);
            return (TResp)resp;
        }
    }
}
