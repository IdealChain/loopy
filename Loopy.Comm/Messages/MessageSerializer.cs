using System.Diagnostics;
using System.Reflection;
using NetMQ;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Loopy.Comm.Messages
{
    public static class MessageSerializer
    {
        private static readonly Dictionary<(RpcOperation type, RpcDirection dir), Type> MessageTypes = new();
        private static readonly Dictionary<Type, (RpcOperation type, RpcDirection dir)> MessageIds = new();

        static MessageSerializer()
        {
            foreach (var msgType in Assembly.GetExecutingAssembly().DefinedTypes)
            {
                var attr = msgType.GetCustomAttribute<RpcMessageAttribute>();
                if (attr == null)
                    continue;

                if (!msgType.IsAssignableTo(typeof(IMessage)))
                    throw new InvalidOperationException($"{msgType} must implement {nameof(IMessage)}");

                var id = (attr.Type, attr.Direction);
                MessageTypes.Add(id, msgType);
                MessageIds.Add(msgType, id);
            }
        }

        public static string GetProtoDefinitions()
        {
            var options = new SchemaGenerationOptions();
            options.Types.Add(typeof(MessageHeader));
            options.Types.AddRange(MessageTypes.Values);
            return Serializer.GetProto(options);
        }

        public static void Serialize(NetMQMessage target, IMessage msg)
        {
            if (!MessageIds.TryGetValue(msg.GetType(), out var id))
                throw new InvalidOperationException($"Unknown msg type {msg.GetType()}");

            var ms = new MemoryStream();
            Serializer.Serialize(ms, new MessageHeader { Type = id.type, Direction = id.dir });
            target.Append(new NetMQFrame(ms.GetBuffer(), (int)ms.Length));

            ms = new MemoryStream();
            Serializer.NonGeneric.Serialize(ms, msg);
            target.Append(new NetMQFrame(ms.GetBuffer(), (int)ms.Length));
        }

        public static IMessage Deserialize(NetMQMessage source, int start = 0)
        {
            if (source.FrameCount - start != 2)
                throw new InvalidOperationException($"Expected 2 frames, got {source.FrameCount - start}");

            var header = Serializer.Deserialize<MessageHeader>(source[start].AsSpan());
            var id = (header.Type, header.Direction);

            if (!MessageTypes.TryGetValue(id, out var messageType))
                throw new InvalidOperationException($"Unknown msg type {header.Type}");

            return (IMessage)Serializer.NonGeneric.Deserialize(messageType, source[start + 1].AsSpan());
        }
    }
}
