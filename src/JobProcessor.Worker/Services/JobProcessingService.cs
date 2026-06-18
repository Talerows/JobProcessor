using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Services;

/// <summary>
/// Simulates job processing with a random duration between 2 and 6 minutes.
/// </summary>
public class JobProcessingService : IJobProcessingService
{
    private static readonly Random _random = Random.Shared;
    private readonly ILogger<JobProcessingService> _logger;

    public JobProcessingService(ILogger<JobProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(Order job, CancellationToken cancellationToken)
    {
        var workDuration = TimeSpan.FromSeconds(_random.Next(
            minValue: (int)TimeSpan.FromMinutes(2).TotalSeconds,
            maxValue: (int)TimeSpan.FromMinutes(6).TotalSeconds));

        _logger.LogInformation(
            "Job {JobId} processing started. Simulated duration: {DurationSeconds}s.",
            job.Id, (int)workDuration.TotalSeconds);

        await Task.Delay(workDuration, cancellationToken);

        _logger.LogInformation("Job {JobId} processing finished.", job.Id);
    }
}
