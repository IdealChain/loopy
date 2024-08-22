using Loopy.Comm.MaelstromMessages;
using Loopy.Comm.Sockets;
using Loopy.Core.Enums;
using Loopy.MaelstromNode;
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
        Layout = new SimpleLayout { Text = @"${logger} ${scopenested}: ${message}" }, StdErr = true,
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
    var rootCommand = new RootCommand("Loopy Data Store Node for Maelstrom");
    var consistencyArg = new Argument<ConsistencyMode>(
        "mode", () => ConsistencyMode.Fifo, "Consistency model for queries");
    var quorumArg = new Argument<int>(
        "quorum", () => 1, "Number of replica nodes to query");

    rootCommand.Add(consistencyArg);
    rootCommand.Add(quorumArg);
    rootCommand.SetHandler((mode, rq) =>
    {
        using (var runtime = new NetMQRuntime())
            runtime.Run(cancellation.Token, RunNode(mode, rq, cancellation.Token));
    }, consistencyArg, quorumArg);
    return rootCommand;
}

static async Task RunNode(ConsistencyMode mode, int readQuorum, CancellationToken ct)
{
    try
    {
        var socket = new MulticastSocket<Envelope>(new JsonSocket<Envelope>(new ConsoleSocket()));
        var api = new MaelstromApi(socket) { ConsistencyMode = mode, ReadQuorum = readQuorum };
        var (nodeId, nodeIds) = await api.WaitForInit(ct);
        await api.RunNode(nodeId, nodeIds, ct);
    }
    catch (Exception e) when (!ct.IsCancellationRequested)
    {
        Console.Error.WriteLine(e.ToString());
    }
}
