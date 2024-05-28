using Loopy.Comm.Messages;
using NetMQ;

namespace Loopy.Comm.Network
{
    internal static class ExtensionMethods
    {
        public static void SendMessage(this NetMQSocket socket, IMessage msg)
        {
            var mq = new NetMQMessage();
            MessageSerializer.Serialize(mq, msg);
            socket.SendMultipartMessage(mq);
        }

        public static async Task<IMessage> ReceiveMessage(this NetMQSocket socket, CancellationToken token)
        {
            var mq = await socket.ReceiveMultipartMessageAsync(2, token);
            return MessageSerializer.Deserialize(mq);
        }

        public static async Task<TResp> RemoteCall<TReq, TResp>(this NetMQSocket socket, TReq msg, CancellationToken token)
            where TReq : IMessage where TResp : IMessage
        {
            socket.SendMessage(msg);
            return (TResp)await socket.ReceiveMessage(token);
        }
    }
}
