using Loopy.Core.Data;

namespace Loopy.Core.Api;

public class BackgroundTaskScheduler
{
    internal BackgroundTaskScheduler(Node node) => Node = node;

    private Node Node { get; }

    public BackgroundTaskConfig Config { get; set; } = new();

    public async Task Run(CancellationToken cancellationToken = default)
    {
        await await Task.WhenAny(
            PeriodicAntiEntropy(cancellationToken),
            PeriodicStripCausality(cancellationToken),
            PeriodicHeartbeat(cancellationToken));
    }

    public async Task PeriodicAntiEntropy(CancellationToken cancellationToken)
    {
        var peers = Node.Context.ReplicationStrategy.GetPeerNodes(Node.Id)
            .Where(n => n != Node.Id)
            .ToArray();

        if (peers.Length == 0)
            return;

        Random.Shared.Shuffle(peers);
        for (var i = 0; !cancellationToken.IsCancellationRequested; i = (i + 1) % peers.Length)
        {
            await Task.Delay(Config.AntiEntropyInterval + RandomJitter(), cancellationToken);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                cts.CancelAfter(Config.AntiEntropyTimeout);
                await AntiEntropy(peers[i], cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                Node.Logger.Warn("anti-entropy with {Peer} timeout", peers[i]);
            }
        }
    }

    public async Task AntiEntropy(NodeId peer, CancellationToken cancellationToken = default)
    {
        try
        {
            await Node.AntiEntropy(peer, cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            Node.Logger.Warn(e, "anti-entropy with {Peer} failed: {Ex}", peer, e);
        }
    }

    public async Task PeriodicStripCausality(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(Config.StripInterval + RandomJitter(), cancellationToken);
            await StripCausality(cancellationToken);
        }
    }

    public async Task StripCausality(CancellationToken cancellationToken = default)
    {
        try
        {
            await Node.StripCausality(cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            Node.Logger.Warn(e, "strip causality failed: {Ex}", e);
        }
    }

    public async Task PeriodicHeartbeat(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(Config.HeartbeatInterval + RandomJitter(), cancellationToken);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                cts.CancelAfter(Config.HeartbeatTimeout);
                await Heartbeat(Config.HeartbeatTolerance, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                Node.Logger.Warn("heartbeat timeout");
            }
        }
    }

    public async Task Heartbeat(TimeSpan tolerance, CancellationToken cancellationToken = default)
    {
        try
        {
            await Node.Heartbeat(tolerance, cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            Node.Logger.Warn(e, "heartbeat failed: {Ex}", e);
        }
    }

    private TimeSpan RandomJitter()
    {
        // a bit of random jitter to prevent the background task periods from aligning their phase
        return TimeSpan.FromTicks(Random.Shared.NextInt64(Config.MaxJitterInterval.Ticks));
    }
}
