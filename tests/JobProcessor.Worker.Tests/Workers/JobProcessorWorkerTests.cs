using FluentAssertions;
using JobProcessor.Worker.Configuration;
using JobProcessor.Worker.Domain;
using JobProcessor.Worker.Infrastructure.Repositories;
using JobProcessor.Worker.Services;
using JobProcessor.Worker.Tests.Helpers;
using JobProcessor.Worker.Workers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace JobProcessor.Worker.Tests.Workers;

[TestFixture]
public sealed class JobProcessorWorkerTests
{
    private Mock<IJobRepository> _repositoryMock = null!;
    private Mock<IJobProcessingService> _processingServiceMock = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<IJobRepository>();
        _processingServiceMock = new Mock<IJobProcessingService>();
    }

    private (JobProcessorWorker worker, JobQueueService queue) BuildWorker(
        Action<JobProcessorOptions>? configure = null)
    {
        var options = OptionsFactory.Create(o =>
        {
            o.PollingIntervalSeconds = 0; // no sleep between cycles in tests
            configure?.Invoke(o);
        });

        var queue = new JobQueueService(options.Value.MaxQueueSize, NullLogger<JobQueueService>.Instance);
        var scopeFactory = ScopeFactoryHelper.Create(_repositoryMock);

        var fetcher = new JobFetcherService(
            scopeFactory.Object,
            queue,
            options,
            NullLogger<JobFetcherService>.Instance);

        var dispatcher = new JobDispatcherService(
            queue,
            _processingServiceMock.Object,
            scopeFactory.Object,
            options,
            NullLogger<JobDispatcherService>.Instance);

        var worker = new JobProcessorWorker(
            fetcher,
            dispatcher,
            options,
            NullLogger<JobProcessorWorker>.Instance);

        return (worker, queue);
    }

    [Test]
    public async Task ExecuteAsync_WhenCancelledImmediately_StopsCleanly()
    {
        var (worker, _) = BuildWorker();

        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        Func<Task> act = () => worker.StartAsync(cts.Token);
        await act.Should().NotThrowAsync();

        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_PollingLoop_CallsRepositoryMultipleTimes()
    {
        var (worker, _) = BuildWorker(o => o.PollingIntervalSeconds = 0);

        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(150); // allow several polling cycles
        await worker.StopAsync(CancellationToken.None);

        _repositoryMock.Verify(
            r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Test]
    public async Task ExecuteAsync_WhenRepositoryThrowsRepeatedly_WorkerKeepsRunning()
    {
        var (worker, _) = BuildWorker(o => o.PollingIntervalSeconds = 0);
        int callCount = 0;

        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await worker.StopAsync(CancellationToken.None);

        // Worker should have retried multiple times without crashing.
        callCount.Should().BeGreaterThan(1);
    }

    [Test]
    public async Task ExecuteAsync_EndToEnd_JobFetchedProcessedAndMarkedCompleted()
    {
        var (worker, _) = BuildWorker(o =>
        {
            o.PollingIntervalSeconds = 0;
            o.MaxParallelJobs = 1;
            o.JobTimeoutMinutes = 1;
        });

        var job = JobFactory.CreateOpen();
        var returned = false;

        // Return the job on the first call only; return empty on subsequent calls.
        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (returned) return Array.Empty<Order>();
                returned = true;
                return new[] { job };
            });

        _processingServiceMock
            .Setup(s => s.ProcessAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(400); // enough for fetch → dispatch → process cycle
        await worker.StopAsync(CancellationToken.None);

        _repositoryMock.Verify(
            r => r.MarkCompletedAsync(job.Id, It.IsAny<DateTime>(), CancellationToken.None),
            Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_AfterStopAsync_WorkerDoesNotCallRepositoryAnymore()
    {
        var (worker, _) = BuildWorker(o => o.PollingIntervalSeconds = 0);
        int callCount = 0;

        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync(Array.Empty<Order>());

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        int countAfterStop = callCount;
        await Task.Delay(100); // worker must not poll anymore

        callCount.Should().Be(countAfterStop,
            "repository should not be called after StopAsync");
    }
}
