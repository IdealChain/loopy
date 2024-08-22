using Loopy.ClientShell;
using Loopy.Comm.Interfaces;
using NetMQ;
using System.CommandLine;

var cancellation = new CancellationTokenSource();
HandleCancelKey(cancellation);
return await BuildCommand(cancellation).InvokeAsync(args);

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
    var rootCommand = new RootCommand("Loopy Client Shell");
    var hostArgument = new Argument<string>("host", () => NetMQRpcDefaults.Localhost(1), "Node to connect to");

    rootCommand.Add(hostArgument);
    rootCommand.SetHandler(host =>
    {
        using (var runtime = new NetMQRuntime())
            runtime.Run(new Shell(host, cancellation.Token).Run());

    }, hostArgument);
    return rootCommand;
}
