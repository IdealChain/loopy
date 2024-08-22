using Loopy.Comm.Interfaces;
using NetMQ;
using ProtoBuf;
using System.Runtime.CompilerServices;

namespace Loopy.Comm.Sockets;

public class NetMQProtobufSocket<T>(NetMQSocket wrapped) : IAsyncSocket<(T msg, NetMQMessage? header)>
{
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public async IAsyncEnumerable<(T msg, NetMQMessage? header)> ReceiveAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested && !wrapped.IsDisposed)
            yield return await ReceiveAsync(ct);
    }

    public async Task<(T msg, NetMQMessage? header)> ReceiveAsync(CancellationToken ct = default)
    {
        var mq = await wrapped.ReceiveMultipartMessageAsync(cancellationToken: ct);
        return Deserialize(mq);
    }

    internal static (T msg, NetMQMessage? header) Deserialize(NetMQMessage mq)
    {
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

        var msg = Serializer.Deserialize<T>(mq[msgStart.GetValueOrDefault(0)].AsSpan());
        return (msg, header);
    }

    public Task SendAsync((T msg, NetMQMessage? header) message, CancellationToken ct = default)
    {
        var mq = Serialize(message);
        if (!wrapped.TrySendMultipartMessage(SendTimeout, mq))
            throw new OperationCanceledException("send timeout");

        return Task.CompletedTask;
    }

    internal static NetMQMessage Serialize((T msg, NetMQMessage? header) message)
    {
        var mq = new NetMQMessage(message.header ?? Enumerable.Empty<NetMQFrame>());

        // append empty frame only if a header was given (request socket prepends its own envelope)
        if (message.header != null)
            mq.AppendEmptyFrame();

        var ms = new MemoryStream();
        Serializer.Serialize(ms, message.msg);
        mq.Append(new NetMQFrame(ms.GetBuffer(), (int)ms.Length));
        return mq;
    }
}
