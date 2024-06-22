using Loopy.Comm.Network;
using Loopy.Data;
using Loopy.NodeShell;
using NetMQ;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using System.CommandLine;

var cancellation = new CancellationTokenSource();
ConfigureLogging();
HandleCancelKey(cancellation);
return await BuildCommand(cancellation).InvokeAsync(args);

static void ConfigureLogging()
{
    var console = new ConsoleTarget("console")
    {
        Layout = new SimpleLayout { Text = @"${logger} ${scopenested}: ${message}" }
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

    rootCommand.Add(nodeIdArgument);
    rootCommand.Add(peersOption);
    rootCommand.SetHandler((nodeId, peers) =>
    {
        using (var runtime = new NetMQRuntime())
            runtime.Run(cancellation.Token, RunNode(nodeId, peers, cancellation.Token));

    }, nodeIdArgument, peersOption);
    return rootCommand;
}

static async Task RunNode(int nodeId, int[] peers, CancellationToken cancellationToken)
{
    var context = new SingleNodeContext(nodeId, peers.Select(p => new NodeId(p)));
    using var nodeServer = new NodeApiServer(context.GetNodeApi(nodeId), context.GetHost(nodeId));
    using var clientServer = new ClientApiServer(context.Node, context.GetHost(nodeId));

    context.Node.Logger.Info("Running.");
    await Task.WhenAll(
        nodeServer.HandleRequests(cancellationToken), clientServer.HandleRequests(cancellationToken));
}
