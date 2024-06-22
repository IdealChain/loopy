using Loopy.Comm.Network;
using Loopy.Data;
using Spectre.Console;
using System.CommandLine;

namespace Loopy.ClientShell;

internal class Shell
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);

    private ClientApiClient _client;
    private CausalContext _causalContext = CausalContext.Initial;
    private readonly CancellationToken _cancellationToken;
    private bool _exit;    

    public Shell(string host, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _client = new ClientApiClient(host);
    }

    public async Task Run()
    {
        var shellCommand = BuildShellCommand();
        while (!_cancellationToken.IsCancellationRequested && !_exit)
        {
            var line = AnsiConsole.Ask<string>($"{_client.Host}>");
            if (line != null && !_cancellationToken.IsCancellationRequested && !_exit)
                await shellCommand.InvokeAsync(line);
        }
    }

    private RootCommand BuildShellCommand()
    {
        var shellCommand = new RootCommand("Data Store Shell");
        var keyArgument = new Argument<string>("key");
        var valueArgument = new Argument<string>("value");
        var hostArgument = new Argument<string>("host", "Node to connect to");

        var getCommand = new Command("get", "Read key's value");
        getCommand.AddAlias("g");
        getCommand.Add(keyArgument);
        getCommand.SetHandler(key => WrapCommand(Get, key), keyArgument);
        shellCommand.Add(getCommand);

        var putCommand = new Command("put", "Update key's value");
        putCommand.AddAlias("p");
        putCommand.Add(keyArgument);
        putCommand.Add(valueArgument);
        putCommand.SetHandler((key, value) => WrapCommand(Put, key, value), keyArgument, valueArgument);
        shellCommand.Add(putCommand);

        var deleteCommand = new Command("delete", "Delete key's value");
        deleteCommand.AddAlias("d");
        deleteCommand.Add(keyArgument);
        deleteCommand.SetHandler(key => WrapCommand(Delete, key), keyArgument);
        shellCommand.Add(deleteCommand);

        var changeNodeCommand = new Command("connect", "Connect to different node");
        changeNodeCommand.Add(hostArgument);
        changeNodeCommand.AddAlias("c");
        changeNodeCommand.SetHandler(ChangeNode, hostArgument);
        shellCommand.Add(changeNodeCommand);

        var exitCommand = new Command("exit", "Quit");
        exitCommand.AddAlias("q");
        exitCommand.SetHandler(() => _exit = true);
        shellCommand.Add(exitCommand);

        return shellCommand;
    }

    private void ChangeNode(string host)
    {
        _client.Dispose();
        _client = new ClientApiClient(host);
        _causalContext = CausalContext.Initial;
    }

    private static async Task WrapCommand<T>(Func<T, CancellationToken, Task<string>> func, T arg)
    {
        using var timeout = new CancellationTokenSource(CommandTimeout);
        await AnsiConsole.Status().StartAsync("Waiting...", async ctx =>
        {
            try
            {
                var result = await func(arg, timeout.Token);
                AnsiConsole.WriteLine(result);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.WriteLine("Timeout.");
            }
        });
    }

    private static async Task WrapCommand<T1, T2>(Func<T1, T2, CancellationToken, Task<string>> func, T1 arg1, T2 arg2)
    {
        await WrapCommand((arg1, ct) => func(arg1, arg2, ct), arg1);
    }

    private async Task<string> Get(string key, CancellationToken cancellationToken)
    {
        var (values, cc) = await _client.Get(key);
        _causalContext = cc;
        return values.Length > 0 ? string.Join(", ", values) : "Empty";
    }

    private async Task<string> Put(string key, string value, CancellationToken cancellationToken)
    {
        await _client.Put(key, value, _causalContext);
        return "OK";
    }

    private async Task<string> Delete(string key, CancellationToken cancellationToken)
    {
        await _client.Delete(key, _causalContext);
        return "OK";
    }
}
