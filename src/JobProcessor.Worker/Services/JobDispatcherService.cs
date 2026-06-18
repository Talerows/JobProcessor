using JobProcessor.Worker.Configuration;
using JobProcessor.Worker.Domain;
using JobProcessor.Worker.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace JobProcessor.Worker.Services;

/// <summary>
/// Dequeues jobs from the <see cref="JobQueueService"/> and dispatches them to parallel workers.
/// </summary>
public sealed class JobDispatcherService : IJobDispatcherService
{
    private readonly IJobQueueService _queue;
    private readonly IJobProcessingService _processingService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobProcessorOptions _options;
    private readonly ILogger<JobDispatcherService> _logger;

    private readonly SemaphoreSlim _semaphore;

    public JobDispatcherService(
        IJobQueueService queue,
        IJobProcessingService processingService,
        IServiceScopeFactory scopeFactory,
        IOptions<JobProcessorOptions> options,
        ILogger<JobDispatcherService> logger)
    {
        _queue = queue;
        _processingService = processingService;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;

        _semaphore = new SemaphoreSlim(_options.MaxParallelJobs, _options.MaxParallelJobs);
    }

    /// <summary>
    /// Drains available jobs from the queue and launches worker tasks until
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public async Task DispatchAvailableJobsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _queue.TryDequeue(out var job))
        {
            await _semaphore.WaitAsync(cancellationToken);

            int activeWorkers = _options.MaxParallelJobs - _semaphore.CurrentCount;
            _logger.LogInformation(
                "Dispatching job {JobId}. ActiveWorkers={ActiveWorkers}/{MaxWorkers}. QueueSize={QueueSize}.",
                job!.Id, activeWorkers, _options.MaxParallelJobs, _queue.Count);

            // Fire-and-forget; semaphore is released inside the task.
            _ = Task.Run(() => RunJobAsync(job, cancellationToken), cancellationToken);
        }
    }

    private async Task RunJobAsync(Job job, CancellationToken hostCancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromMinutes(_options.JobTimeoutMinutes));

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            hostCancellationToken, timeoutCts.Token);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        try
        {
            _logger.LogInformation("Job {JobId} ({JobName}) is now InProgress.", job.Id, job.Name);
            await _processingService.ProcessAsync(job, linkedCts.Token);

            var completedAt = DateTime.UtcNow;
            await repository.MarkCompletedAsync(job.Id, completedAt, CancellationToken.None);

            _logger.LogInformation(
                "Job {JobId} ({JobName}) completed successfully at {CompletedAt:O}.",
                job.Id, job.Name, completedAt);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            await HandleTimeoutAsync(job, repository);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} ({JobName}) failed with an unhandled exception.", job.Id, job.Name);
        }
        finally
        {
            _semaphore.Release();

            int activeWorkers = _options.MaxParallelJobs - _semaphore.CurrentCount;
            _logger.LogInformation(
                "Semaphore released for job {JobId}. ActiveWorkers={ActiveWorkers}/{MaxWorkers}.",
                job.Id, activeWorkers, _options.MaxParallelJobs);
        }
    }

    private async Task HandleTimeoutAsync(Job job, IJobRepository repository)
    {
        var timedOutAt = DateTime.UtcNow;
        _logger.LogWarning(
            "Job {JobId} ({JobName}) timed out after {TimeoutMinutes} minutes at {TimedOutAt:O}.",
            job.Id, job.Name, _options.JobTimeoutMinutes, timedOutAt);

        try
        {
            await repository.MarkTimedOutAsync(job.Id, timedOutAt, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist Timeout status for job {JobId}. Manual intervention may be required.",
                job.Id);
        }
    }
}