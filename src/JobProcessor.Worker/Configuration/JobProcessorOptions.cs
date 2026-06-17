namespace JobProcessor.Worker.Configuration;

/// <summary>
/// Strongly-typed configuration bound from appsettings.json → "JobProcessor" section.
/// </summary>
public sealed class JobProcessorOptions
{
    public const string SectionName = "JobProcessor";

    /// <summary>Interval between database polls for open jobs.</summary>
    public int PollingIntervalSeconds { get; set; } = 10;

    /// <summary>Maximum number of jobs that can sit in the internal queue at once.</summary>
    public int MaxQueueSize { get; set; } = 50;

    /// <summary>Number of jobs that may be processed in parallel.</summary>
    public int MaxParallelJobs { get; set; } = 5;

    /// <summary>
    /// Maximum duration (minutes) a single job may run before it is considered timed out.
    /// </summary>
    public int JobTimeoutMinutes { get; set; } = 5;

    /// <summary>PostgreSQL connection string key looked up from ConnectionStrings section.</summary>
    public string ConnectionStringName { get; set; } = "JobProcessor";
}
