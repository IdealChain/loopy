using Loopy.Data;

namespace Loopy
{
    public class LocalBackgroundTasks(Node node)
    {
        public TimeSpan AntiEntropyInterval { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan AntiEntropyTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public TimeSpan StripInterval { get; set; } = TimeSpan.FromSeconds(90);

        private TimeSpan RandomJitter() => TimeSpan.FromMilliseconds(Random.Shared.Next(5000));

        public async Task Run(CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(PeriodicAntiEntropy(cancellationToken), PeriodicStripCausality(cancellationToken));
        }

        public async Task PeriodicAntiEntropy(CancellationToken cancellationToken)
        {
            var peers = node.Context.GetPeerNodes(node.Id).Where(n => n != node.Id).ToArray();
            Random.Shared.Shuffle(peers);

            if (peers.Length == 0)
                return;

            for (var i = 0; !cancellationToken.IsCancellationRequested; i = (i + 1) % peers.Length)
            {
                await Task.Delay(AntiEntropyInterval + RandomJitter(), cancellationToken);
                using var timeoutSource = new CancellationTokenSource(AntiEntropyTimeout);

                try
                {
                    using var combinedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
                    await AntiEntropy(peers[i], combinedSource.Token);
                }
                catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
                {
                    node.Logger.Warn("anti-entropy with {Peer} timeout", peers[i]);
                }
            }
        }

        public async Task AntiEntropy(NodeId peer, CancellationToken cancellationToken = default)
        {
            try
            {
                using (await node.NodeLock.EnterAsync(cancellationToken))
                    await node.AntiEntropy(peer, cancellationToken);
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                node.Logger.Warn("anti-entropy with {Peer} failed: {Message}", peer, e.Message);
            }
        }

        public async Task PeriodicStripCausality(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(StripInterval + RandomJitter(), cancellationToken);
                await StripCausality(cancellationToken);
            }
        }

        public async Task StripCausality(CancellationToken cancellationToken = default)
        {
            try
            {
                using (await node.NodeLock.EnterAsync(cancellationToken))
                    await node.StripCausality();
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                node.Logger.Warn("strip causality failed: {Message}", e.Message);
            }
        }
    }
}
