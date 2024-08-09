using Loopy.Comm.Network;
using Loopy.Core;
using Loopy.Core.Data;
using Loopy.Node;
using NetMQ;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using System.CommandLine;
using System.Diagnostics;

var cancellation = new CancellationTokenSource();
ConfigureLogging();
HandleCancelKey(cancellation);
return await BuildCommand(cancellation).InvokeAsync(args);

static void ConfigureLogging()
{
    var console = new ConsoleTarget("console")
    {
        Layout = new SimpleLayout { Text = @"${logger} ${scopenested}: ${message}" },
        StdErr = true,
    };

    var config = new NLog.Config.LoggingConfiguration();
    config.AddRule(LogLevel.Trace, LogLevel.Fatal, console);
    LogManager.Configuration = config;
}

static void HandleCancelKey(CancellationTokenSource cancellation)
{
    Console.CancelKeyPress += (_, e) =>
    {
        if (cancellation.IsCancellationRequested)
            Environment.Exit(-1);

        cancellation.Cancel();
        e.Cancel = true;
    };
}

static RootCommand BuildCommand(CancellationTokenSource cancellation)
{
    var rootCommand = new RootCommand("Loopy Data Store Node");
    var nodeIdArgument = new Argument<int>("id", "Node ID");
    var peersOption = new Option<int[]>("--peers", "Peer Node IDs") { AllowMultipleArgumentsPerToken = true };
    var launchPeersOption = new Option<bool>("--launch-peers", "Start Peer Nodes, too");

    rootCommand.Add(nodeIdArgument);
    rootCommand.Add(peersOption);
    rootCommand.Add(launchPeersOption);
    rootCommand.SetHandler((nodeId, peers, launchPeers) =>
    {
        if (launchPeers)
            LaunchPeers(nodeId, peers);

        using (var runtime = new NetMQRuntime())
            runtime.Run(cancellation.Token, RunNode(nodeId, peers, cancellation.Token));
    }, nodeIdArgument, peersOption, launchPeersOption);
    return rootCommand;
}

static async Task RunNode(int nodeId, int[] peers, CancellationToken cancellationToken)
{
    using var context = new SingleNodeContext(nodeId, peers.Select(p => new NodeId(p)));

    try
    {
        var nodeServer = new NodeApiServer(new LocalNodeApi(context.Node), context.GetHost(nodeId));
        var clientServer = new ClientApiServer(new LocalClientApi(context.Node), context.GetHost(nodeId));
        var backgroundTasks = new LocalBackgroundTasks(context.Node);

        await Task.WhenAll(
            nodeServer.HandleRequests(cancellationToken),
            clientServer.HandleRequests(cancellationToken),
            backgroundTasks.Run(cancellationToken));
    }
    catch (Exception e) when (!cancellationToken.IsCancellationRequested)
    {
        context.Node.Logger.Fatal(e, e.Message);
    }
}

static void LaunchPeers(int nodeId, int[] peers)
{
    var exe = Environment.ProcessPath;
    if (string.IsNullOrEmpty(exe))
        return;

    foreach (var peer in peers)
    {
        var nodePeers = peers.Prepend(nodeId).Where(n => n != peer);
        var args = $"{peer} --peers {string.Join(" ", nodePeers)}";
        Process.Start(exe, args);
    }
}
