using CliWrap;
using CliWrap.Builders;
using CliWrap.EventStream;
using NUnit.Framework;

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

    private void AddResult(string clientMode, int rate, int latency, string nemesis, bool consistent)
    {
        void AddRow(params object[] values)
        {
            _results.Add(string.Join(" & ", values) + @" \\");
            TestContext.Out.WriteLine(_results.Last());
        }

        if (_results.Count == 0)
            _results.Add(@"\midrule");

        AddRow(clientMode,
            rate,
            latency,
            string.IsNullOrEmpty(nemesis) ? "---" : nemesis,
            consistent ? @"\consistent" : @"\inconsistent");
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
        [Values(500)] int latency,
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
        }

        var maelstromResult = await RunWithOutput(Cli.Wrap("lein")
            .WithArguments(BuildArgs)
            .WithWorkingDirectory(_maelstromDir)
            .WithValidation(CommandResultValidation.None));
        Assert.That(maelstromResult, Is.InRange(0, 1));

        var resultDir = Path.Combine(_maelstromDir, "store", Workload, "latest");
        resultDir = Directory.ResolveLinkTarget(resultDir, true)?.FullName;
        var historyFile = Path.Combine(resultDir, "history.edn");
        Assert.That(File.Exists(historyFile), $"{historyFile} doesn't exist");

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

        AddResult(clientMode, rate, latency, nemesis, isConsistent);
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

    private static async Task<int> RunWithOutput(Command cmd)
    {
        var exitCode = -1;
        await foreach (var cmdEvent in cmd.ListenAsync())
        {
            switch (cmdEvent)
            {
                case StartedCommandEvent started:
                    TestContext.Out.WriteLine($"Process started; ID: {started.ProcessId}; {cmd.TargetFilePath} {cmd.Arguments}");
                    break;
                case StandardOutputCommandEvent stdOut when LogOutput:
                    TestContext.Out.WriteLine(stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr when !stdErr.Text.StartsWith("warning:", StringComparison.OrdinalIgnoreCase):
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
