using NLog;
using NLog.Layouts;
using NLog.Targets;

namespace Loopy.Test;

[SetUpFixture]
public class SetupTestEnvironment
{
    [OneTimeSetUp]
    public void StartTest()
    {
        // enable Trace logging to test output
        var console = new ConsoleTarget("console")
        {
            Layout = new SimpleLayout { Text = @"${logger} ${scopenested}: ${message}" }
        };
        
        var config = new NLog.Config.LoggingConfiguration();
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, console);
        LogManager.Configuration = config;
    }

    [OneTimeTearDown]
    public void EndTest()
    {
        LogManager.Flush();
    }
}
