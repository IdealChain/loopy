using NLog;
using NLog.Layouts;
using NLog.Targets;

namespace Loopy.Test;

[SetUpFixture]
public class SetupTrace
{
    [OneTimeSetUp]
    public void StartTest()
    {
        var console = new ConsoleTarget("console")
        {
            Layout = new SimpleLayout { Text = "${event-properties:node}: ${message}" }
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