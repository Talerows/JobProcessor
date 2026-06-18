using JobProcessor.Worker.Configuration;
using JobProcessor.Worker.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobProcessor.Worker.Workers;

/// <summary>
/// The hosted <see cref="BackgroundService"/> that ties together fetching and dispatching.
///
/// Loop per polling interval:
///   1. <see cref="IJobFetcherService.FetchAndEnqueueAsync"/> – claim open jobs from PostgreSQL.
///   2. <see cref="IJobDispatcherService.DispatchAvailableJobsAsync"/> – send queued jobs to workers.
///   3. Wait for the next polling interval (or exit on cancellation).
/// </summary>
public sealed class JobProcessorWorker : BackgroundService
{
    private readonly IJobFetcherService _fetcher;
    private readonly IJobDispatcherService _dispatcher;
    private readonly JobProcessorOptions _options;
    private readonly ILogger<JobProcessorWorker> _logger;

    public JobProcessorWorker(
        IJobFetcherService fetcher,
        IJobDispatcherService dispatcher,
        IOptions<JobProcessorOptions> options,
        ILogger<JobProcessorWorker> logger)
    {
        _fetcher    = fetcher;
        _dispatcher = dispatcher;
        _options    = options.Value;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "JobProcessorWorker started. PollingInterval={PollingIntervalSeconds}s, " +
            "MaxParallelJobs={MaxParallelJobs}, MaxQueueSize={MaxQueueSize}, " +
            "JobTimeoutMinutes={JobTimeoutMinutes}.",
            _options.PollingIntervalSeconds,
            _options.MaxParallelJobs,
            _options.MaxQueueSize,
            _options.JobTimeoutMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {

            try
            {
                await _fetcher.FetchAndEnqueueAsync(stoppingToken);
                await _dispatcher.DispatchAvailableJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException e)
            {
                // Normal shutdown path – break out of the loop cleanly.
                _logger.LogWarning(e, "Operation Canceled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in the main processing loop. Waiting before retry.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("JobProcessorWorker stopped.");
    }
}
