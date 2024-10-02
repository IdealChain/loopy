using CliWrap;
using CliWrap.Builders;
using CliWrap.EventStream;
using Loopy.Comm.MaelstromMessages;
using NUnit.Framework;
using MathNet.Numerics.Statistics;
using System.Diagnostics;

namespace Loopy.MaelstromTest;

[TestFixture, Explicit]
public class Test
{
    private const bool LogOutput = false;

    // constant settings for the whole run
    private const string Workload = "fifo-kv";
    private const int ReadQuorum = 1;
    private const int NodeCount = 4;
    private const int TimeLimit = 60;

    private string _solutionDir;
    private string _nodeBin;
    private string _maelstromDir;
    private string _checkerDir;
    private readonly List<string> _results = new();

    [OneTimeSetUp]
    public async Task Prepare()
    {
        // find tool paths
        var path = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (path.Parent != null && !path.EnumerateFiles("*.sln").Any())
            path = path.Parent;
        _solutionDir = path.FullName;
        _maelstromDir = Path.Combine(_solutionDir, "..", "maelstrom");
        _checkerDir = Path.Combine(_solutionDir, "..", "Checker");

        if (!Directory.Exists(_maelstromDir))
            Assert.Inconclusive("Maelstrom directory doesn't exist");
        if (!Directory.Exists(_checkerDir))
            Assert.Inconclusive("Checker directory doesn't exist");

        // ensure node build is up to date
        await RunWithOutput(Cli.Wrap("dotnet")
            .WithArguments("build Loopy.MaelstromNode -c Release")
            .WithWorkingDirectory(_solutionDir));
        _nodeBin = Path.Combine(_solutionDir, "Loopy.MaelstromNode/bin/Release/net8.0/Loopy.MaelstromNode");
        Assert.That(File.Exists(_nodeBin), "Node binary not found");
    }

    private void AddResult(
        string clientMode, int targetRate, int targetLatency, string nemesis,
        int readsOk, int writesOk, double? ownLatency, double? otherLatency, bool consistent)
    {
        void AddRow(params object[] values)
        {
            _results.Add(string.Join(" & ", values) + @" \\");
            TestContext.Out.WriteLine(_results.Last());
        }

        if (_results.Count == 0)
            _results.Add(@"\midrule");

        AddRow(
            $@"\mode{clientMode.ToLower()}",
            $"${targetRate}$",
            $"${targetLatency}$",
            $@"\nemesis{(string.IsNullOrEmpty(nemesis) ? "none" : nemesis.ToLower())}",
            $"${1.0 * readsOk / TimeLimit:F1}$ / ${1.0 * writesOk / TimeLimit:F1}$",
            ownLatency.HasValue && otherLatency.HasValue ? $@"${ownLatency:F0}$ / ${otherLatency:F0}$" : "---",
            $@"\consistency{(consistent ? "ok" : "nok")}");
    }

    [OneTimeTearDown]
    public void WriteResults()
    {
        _results.Add(string.Empty);
        File.AppendAllText($"{Workload}-rq{ReadQuorum}.tex", string.Join(Environment.NewLine, _results));
    }

    [Test, Explicit]
    public async Task RunTest(
        [Values("Eventual", "Fifo")] string clientMode,
        [Values(5000)] int rate,
        [Values(0, 250)] int latency,
        [Values("", "partition")] string nemesis)
    {
        void BuildArgs(ArgumentsBuilder builder)
        {
            builder.Add("run test --bin", false);
            builder.Add(_nodeBin);
            builder.Add(clientMode);
            builder.Add(ReadQuorum);
            builder.Add($"-w {Workload}", false);
            builder.Add($"--node-count {NodeCount}", false);
            builder.Add($"--concurrency {NodeCount * 4}", false);
            builder.Add($"--rate {rate}", false);
            builder.Add($"--latency {latency}", false);
            builder.Add($"--time-limit {TimeLimit}", false);
            if (!string.IsNullOrEmpty(nemesis))
                builder.Add($"--nemesis {nemesis}", false);
            builder.Add("--log-net-send");
            builder.Add("--log-net-recv");
        }

        var maelstromResult = await RunWithOutput(Cli.Wrap("lein")
            .WithArguments(BuildArgs)
            .WithWorkingDirectory(_maelstromDir)
            .WithValidation(CommandResultValidation.None));
        Assert.That(maelstromResult, Is.InRange(0, 1));

        var resultDir = Path.Combine(_maelstromDir, "store", Workload, "latest");
        resultDir = Directory.ResolveLinkTarget(resultDir, true)?.FullName;

        var historyFile = Path.Combine(resultDir, "history.edn");
        var logFile = Path.Combine(resultDir, "jepsen.log");
        Assert.That(File.Exists(historyFile), $"{historyFile} doesn't exist");
        Assert.That(File.Exists(logFile), $"{logFile} doesn't exist");

        var logResults = ParseLogfile(logFile);
        double? ownLatency = logResults.OwnLatencies.Any() ? Statistics.Median(logResults.OwnLatencies) : null;
        double? otherLatency = logResults.OtherLatencies.Any() ? Statistics.Median(logResults.OtherLatencies) : null;

        bool isConsistent;
        switch (Workload)
        {
            case "lin-kv":
                isConsistent = maelstromResult == 0;
                break;
            case "fifo-kv":
                isConsistent = await CheckPramConsistency(historyFile);
                break;
            default: Assert.Fail($"Unknown workload {Workload}");
        }

        AddResult(
            clientMode, rate, latency, nemesis,
            logResults.ReadOk,
            logResults.WriteOk,
            ownLatency,
            otherLatency,
            isConsistent);
    }

    private async Task<bool> CheckPramConsistency(string historyFile)
    {
        var jarFile = Path.Combine(_checkerDir, "target/consistency-1.0-SNAPSHOT-jar-with-dependencies.jar");
        var resultCode = await RunWithOutput(Cli.Wrap("java")
            .WithArguments(["-jar", jarFile, historyFile])
            .WithValidation(CommandResultValidation.None));

        Assert.That(resultCode, Is.InRange(0, 1));
        return resultCode == 0;
    }

    private class ValueTimestamps
    {
        public string Owner { get; init; } = string.Empty;
        public DateTimeOffset Invoked { get; init; }
        public DateTimeOffset? Received { get; set; }
        public Dictionary<string, DateTimeOffset> FirstVisible { get; set; } = new();
    }

    // [Test]
    // public void TestParse()
    // {
    //     var latencies = ParseLogfile("/home/daniel/thesis/maelstrom/store/lin-kv/20240929T231834.665+0200/jepsen.log");
    // }

    private class LogResults
    {
        public List<double> OwnLatencies = new();
        public List<double> OtherLatencies = new();
        public int ReadOk = 0;
        public int WriteOk = 0;
    }

    private LogResults ParseLogfile(string logFile)
    {
        var values = new Dictionary<string, ValueTimestamps>();
        var results = new LogResults();
        using (var stream = File.OpenRead(logFile))
        {
            foreach (var msg in JepsenLogMessageParser.Parse(stream))
            {
                if (msg.env?.body is WriteRequest write && Workload == "fifo-kv")
                {
                    // sent from checker to node: write invoked or received
                    if (msg.env.src.StartsWith("c") && msg.env.dest.StartsWith("n"))
                    {
                        if (msg.dir == JepsenLogMessageParser.Direction.send)
                            values.Add(write.value, new ValueTimestamps { Owner = msg.env.dest, Invoked = msg.ts });
                        else if (msg.dir == JepsenLogMessageParser.Direction.recv)
                            values[write.value].Received = msg.ts;
                    }
                    // sent from node to lin-kv: value visible
                    else if (msg.env.src.StartsWith("n") && msg.env.dest == "lin-kv")
                    {
                        if (values.ContainsKey(write.value) &&
                            !values[write.value].FirstVisible.ContainsKey(msg.env.src))
                        {
                            values[write.value].FirstVisible.Add(msg.env.src, msg.ts);
                        }
                    }
                }
                else if (msg.env?.body is ReadOkResponse)
                {
                    // sent from node to checker
                    if (msg.dir == JepsenLogMessageParser.Direction.send &&
                        msg.env.src.StartsWith("n") && msg.env.dest.StartsWith("c"))
                        results.ReadOk++;

                }
                else if (msg.env?.body is WriteOkResponse || msg.env?.body is CasOkResponse)
                {
                    // sent from node to checker
                    if (msg.dir == JepsenLogMessageParser.Direction.send &&
                        msg.env.src.StartsWith("n") && msg.env.dest.StartsWith("c"))
                        results.WriteOk++;
                }
            }
        }

        // collect latencies until first visible
        foreach (var stats in values.Values)
        {
            foreach (var (node, visible) in stats.FirstVisible)
            {
                Assert.That(visible >= stats.Invoked);
                var latency = (visible - stats.Invoked).TotalMilliseconds;
                var list = node == stats.Owner ? results.OwnLatencies : results.OtherLatencies;
                list.Add(latency);
            }
        }
        return results;
    }

    private static async Task<int> RunWithOutput(Command cmd)
    {
        var exitCode = -1;
        await foreach (var cmdEvent in cmd.ListenAsync())
        {
            switch (cmdEvent)
            {
                case StartedCommandEvent started:
                    TestContext.Out.WriteLine(
                        $"Process started; ID: {started.ProcessId}; {cmd.TargetFilePath} {cmd.Arguments}");
                    break;
                case StandardOutputCommandEvent stdOut when LogOutput:
                    TestContext.Out.WriteLine(stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr
                    when !stdErr.Text.StartsWith("warning:", StringComparison.OrdinalIgnoreCase):
                    TestContext.Error.WriteLine(stdErr.Text);
                    break;
                case ExitedCommandEvent exited:
                    TestContext.Out.WriteLine($"Process exited; Code: {exited.ExitCode}");
                    exitCode = exited.ExitCode;
                    break;
            }
        }

        return exitCode;
    }
}
