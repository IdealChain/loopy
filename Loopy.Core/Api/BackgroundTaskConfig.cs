namespace Loopy.Core.Api;

public class BackgroundTaskConfig
{
    public TimeSpan AntiEntropyInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan AntiEntropyTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan StripInterval { get; set; } = TimeSpan.FromSeconds(90);

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan HeartbeatTolerance { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxJitterInterval { get; set; } = TimeSpan.FromSeconds(3);
}
