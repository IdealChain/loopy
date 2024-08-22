using Loopy.Comm.Interfaces;
using Loopy.Comm.NdcMessages;
using Loopy.Comm.Rpc;
using Loopy.Comm.Sockets;
using Loopy.Core.Data;
using Loopy.Core.Enums;
using Spectre.Console;
using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Loopy.ClientShell;

internal class Shell
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    private RpcClientApi _remoteClient;
    private readonly CancellationToken _cancellationToken;
    private string _host;
    private CausalContext _causalContext = CausalContext.Initial;
    private bool _exit;

    public Shell(string host, CancellationToken cancellationToken)
    {
        _host = host;
        _cancellationToken = cancellationToken;

        ChangeNode(host);
    }

    public async Task Run()
    {
        var shellCommand = BuildShellCommand();
        while (!_cancellationToken.IsCancellationRequested && !_exit)
        {
            var line = AnsiConsole.Ask<string>(GetPrompt());
            if (!_cancellationToken.IsCancellationRequested && !_exit)
                await shellCommand.InvokeAsync(line);
        }
    }

    private string GetPrompt()
    {
        var prompt = new StringBuilder(_host);
        prompt.AppendFormat("/{0}", _remoteClient.ConsistencyMode);

        if (_remoteClient.ReadQuorum > 1)
            prompt.AppendFormat("/rq{0}", _remoteClient.ReadQuorum);

        prompt.Append(">");
        return prompt.ToString();
    }

    private RootCommand BuildShellCommand()
    {
        var shellCmd = new RootCommand("Data Store Shell");
        var keyArg = new Argument<string>("key");
        var valueArg = new Argument<string>("value");
        var hostArg = new Argument<string>("host", "Node to connect to");
        var consistencyArg = new Argument<ConsistencyMode>("mode", "Consistency model for queries");
        var quorumArg = new Argument<int>("quorum", "Number of replica nodes to query");

        var getCmd = new Command("get", "Read key's value");
        getCmd.AddAlias("g");
        getCmd.Add(keyArg);
        getCmd.SetHandler(key => WrapCommand(Get, key), keyArg);
        shellCmd.Add(getCmd);

        var putCmd = new Command("put", "Update key's value");
        putCmd.AddAlias("p");
        putCmd.Add(keyArg);
        putCmd.Add(valueArg);
        putCmd.SetHandler((key, value) => WrapCommand(Put, key, value), keyArg, valueArg);
        shellCmd.Add(putCmd);

        var deleteCmd = new Command("del", "Delete key's value");
        deleteCmd.AddAlias("d");
        deleteCmd.Add(keyArg);
        deleteCmd.SetHandler(key => WrapCommand(Delete, key), keyArg);
        shellCmd.Add(deleteCmd);

        var showCausalContextCmd = new Command("cc", "Display current causal context");
        showCausalContextCmd.SetHandler(ShowCausalContext);
        shellCmd.Add(showCausalContextCmd);

        var setConsistencyCmd = new Command("cm", "Set consistency model");
        setConsistencyCmd.Add(consistencyArg);
        setConsistencyCmd.SetHandler(SetConsistency, consistencyArg);
        shellCmd.Add(setConsistencyCmd);

        var setQuorumCmd = new Command("rq", "Set replica read quorum");
        setQuorumCmd.Add(quorumArg);
        setQuorumCmd.SetHandler(SetQuorum, quorumArg);
        shellCmd.Add(setQuorumCmd);

        var changeNodeCmd = new Command("connect", "Connect to different node");
        changeNodeCmd.Add(hostArg);
        changeNodeCmd.AddAlias("c");
        changeNodeCmd.SetHandler(ChangeNode, hostArg);
        shellCmd.Add(changeNodeCmd);

        var exitCmd = new Command("quit", "Quit");
        exitCmd.AddAlias("q");
        exitCmd.AddAlias("exit");
        exitCmd.SetHandler(() => _exit = true);
        shellCmd.Add(exitCmd);

        return shellCmd;
    }

    private static async Task WrapCommand<T>(Func<T, CancellationToken, Task<string>> func, T arg)
    {
        using var timeout = new CancellationTokenSource(CommandTimeout);
        await AnsiConsole.Status().StartAsync("Waiting...", async ctx =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var result = await func(arg, timeout.Token);
                AnsiConsole.WriteLine($"{result} ({sw.ElapsedMilliseconds} ms)");
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.WriteLine("Timeout");
            }
        });
    }

    private async Task WrapCommand<T1, T2>(Func<T1, T2, CancellationToken, Task<string>> func, T1 arg1, T2 arg2)
    {
        await WrapCommand((arg1, ct) => func(arg1, arg2, ct), arg1);
    }

    private async Task<string> Get(string key, CancellationToken cancellationToken)
    {
        var (values, cc) = await _remoteClient.Get(key, cancellationToken: cancellationToken);
        _causalContext.MergeIn(cc, Math.Max);
        return values.Length > 0 ? values.AsCsv() : "Empty";
    }

    private async Task<string> Put(string key, string value, CancellationToken cancellationToken)
    {
        await _remoteClient.Put(key, value, _causalContext, cancellationToken: cancellationToken);
        return "OK";
    }

    private async Task<string> Delete(string key, CancellationToken cancellationToken)
    {
        await _remoteClient.Delete(key, _causalContext, cancellationToken: cancellationToken);
        return "OK";
    }

    private void SetConsistency(ConsistencyMode mode) => _remoteClient.ConsistencyMode = mode;

    private void SetQuorum(int quorum) => _remoteClient.ReadQuorum = quorum;

    private void ShowCausalContext()
    {
        foreach (var kv in _causalContext)
            AnsiConsole.WriteLine("{0}: {1}", kv.Key, kv.Value);
    }

    [MemberNotNull(nameof(_remoteClient), nameof(_host), nameof(_causalContext))]
    private void ChangeNode(string host)
    {
        // expand node ID shorthand
        if (int.TryParse(host, out var id) && id < 255)
            host = NetMQRpcDefaults.Localhost(id);

        _host = host;
        _causalContext = CausalContext.Initial;

        _remoteClient?.Dispose();
        _remoteClient = new RpcClientApi(new NetMQRpcClient<NdcMessage>(host, NetMQRpcDefaults.ClientApiPort));
    }
}
