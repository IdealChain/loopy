using NLog;
using NLog.Layouts;
using NLog.Targets;
using NUnit.Framework;

namespace Loopy.Test;

[SetUpFixture]
public class ConfigureLogging
{
    [OneTimeSetUp]
    public void Setup()
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
    public void TearDown() => LogManager.Flush();
}
