using Loopy.Comm.Interfaces;
using Loopy.Comm.NdcMessages;
using Loopy.Comm.Rpc;
using Loopy.Comm.Sockets;
using Loopy.Core.Api;
using Loopy.Core.Data;
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
    config.AddRule(LogLevel.Debug, LogLevel.Fatal, console);
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
    var replicationStrategy = new GlobalReplicationStrategy(peers.Prepend(nodeId).Select(p => new NodeId(p)));
    var context = new NodeContext(nodeId, replicationStrategy,
        id => new RpcNodeApi(new NetMQRpcClient<NdcMessage>(id, NetMQRpcDefaults.NodeApiPort)));

    try
    {
        var nodeServer = new NetMQRpcServer<NdcMessage>(NetMQRpcDefaults.NodeApiPort, nodeId);
        var clientServer = new NetMQRpcServer<NdcMessage>(NetMQRpcDefaults.ClientApiPort, nodeId);

        await await Task.WhenAny(
            nodeServer.ServeAsync(new RpcNodeApiHandler(context.GetNodeApi()), cancellationToken),
            clientServer.ServeAsync(new RpcClientApiHandler(context.GetClientApi()), cancellationToken),
            context.BackgroundTasks.Run(cancellationToken));
    }
    catch (Exception e) when (!cancellationToken.IsCancellationRequested)
    {
        context.Logger.Fatal(e, e.ToString());
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
