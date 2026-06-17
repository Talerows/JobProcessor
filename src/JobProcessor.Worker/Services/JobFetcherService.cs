using JobProcessor.Worker.Configuration;
using JobProcessor.Worker.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace JobProcessor.Worker.Services;

/// <summary>
/// Periodically polls the database for open jobs and pushes them into the <see cref="JobQueue"/>.
/// </summary>
public sealed class JobFetcherService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobQueue _queue;
    private readonly JobProcessorOptions _options;
    private readonly ILogger<JobFetcherService> _logger;

    public JobFetcherService(
        IServiceScopeFactory scopeFactory,
        JobQueue queue,
        IOptions<JobProcessorOptions> options,
        ILogger<JobFetcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }
    public async Task FetchAndEnqueueAsync(CancellationToken cancellationToken)
    {
        if (_queue.IsFull)
        {
            _logger.LogInformation(
                "Queue is at maximum capacity ({MaxQueueSize}). Skipping fetch cycle.",
                _options.MaxQueueSize);
            return;
        }

        int availableSlots = _options.MaxQueueSize - _queue.Count;

        IReadOnlyList<Domain.Job> jobs;
        try
        {
            // Create a short-lived scope so the transient repository (and its connection)
            // is disposed as soon as the fetch is done.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

            jobs = await repository.ClaimOpenJobsAsync(availableSlots, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch jobs from the database.");
            return;
        }

        _logger.LogInformation("Fetch cycle: {JobCount} open job(s) found and claimed.", jobs.Count);

        int enqueued = 0;
        int skipped = 0;

        foreach (var job in jobs)
        {
            if (_queue.TryEnqueue(job))
            {
                _logger.LogInformation("Job {JobId} ({JobName}) accepted into queue.", job.Id, job.Name);
                enqueued++;
            }
            else
            {
                // Rare: the queue may have filled between the capacity check and here.
                _logger.LogWarning("Job {JobId} ({JobName}) was skipped (queue full or duplicate).", job.Id, job.Name);
                skipped++;
            }
        }

        _logger.LogInformation(
            "Fetch cycle complete. Enqueued={Enqueued}, Skipped={Skipped}, QueueSize={QueueSize}.",
            enqueued, skipped, _queue.Count);
    }
}