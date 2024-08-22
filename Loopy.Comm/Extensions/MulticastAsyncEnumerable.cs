using System.Collections.Immutable;
using System.Threading.Channels;

namespace Loopy.Comm.Extensions;

/// <summary>
/// Represents a multi-cast <see cref="IAsyncEnumerable{T}"/> where
/// each reader can consume the <typeparamref name="T"/> items
/// at its own pace.
/// </summary>
/// <typeparam name="T">The item type produced by the enumerable.</typeparam>
/// <remarks>https://gist.github.com/egil/c517eba3aacb60777e629eff4743c80a</remarks>
public sealed class MulticastAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly UnboundedChannelOptions _channelOptions =
        new() { AllowSynchronousContinuations = false, SingleReader = false, SingleWriter = true, };

    private readonly object _activeChannelsLock = new();
    private ImmutableArray<Channel<T>> _activeChannels = ImmutableArray<Channel<T>>.Empty;

    /// <summary>
    /// Writes the <paramref name="item"/> to any readers.
    /// </summary>
    /// <param name="item">The item to write.</param>
    public void Write(T item)
    {
        foreach (var channel in _activeChannels)
            channel.Writer.TryWrite(item);
    }

    /// <summary>
    /// Mark all <see cref="IAsyncEnumerable{T}"/> streams as completed.
    /// </summary>
    public void Complete()
    {
        var channels = _activeChannels;

        lock (_activeChannelsLock)
            _activeChannels = ImmutableArray<Channel<T>>.Empty;

        foreach (var channel in channels)
            channel.Writer.TryComplete();
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var reader = Subscribe();
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out T? item))
                    yield return item;
            }
        }
        finally { Unsubscribe(reader); }
    }

    private ChannelReader<T> Subscribe()
    {
        var channel = Channel.CreateUnbounded<T>(_channelOptions);

        lock (_activeChannelsLock)
            _activeChannels = _activeChannels.Add(channel);

        return channel.Reader;
    }

    private void Unsubscribe(ChannelReader<T> reader)
    {
        if (_activeChannels.FirstOrDefault(x => ReferenceEquals(x.Reader, reader)) is not Channel<T> channel)
            return;

        lock (_activeChannelsLock)
            _activeChannels = _activeChannels.Remove(channel);

        channel.Writer.TryComplete();
    }
}
