using FluentAssertions;
using JobProcessor.Worker.Configuration;
using JobProcessor.Worker.Domain;
using JobProcessor.Worker.Infrastructure.Repositories;
using JobProcessor.Worker.Services;
using JobProcessor.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace JobProcessor.Worker.Tests.Services;

[TestFixture]
public sealed class JobDispatcherServiceTests
{
    private Mock<IJobRepository> _repositoryMock = null!;
    private Mock<IJobProcessingService> _processingServiceMock = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryMock        = new Mock<IJobRepository>();
        _processingServiceMock = new Mock<IJobProcessingService>();
    }

    private (JobDispatcherService dispatcher, JobQueueService queue) BuildDispatcher(
        Action<JobProcessorOptions>? configure = null)
    {
        var queue        = new JobQueueService(20, NullLogger<JobQueueService>.Instance);
        var scopeFactory = ScopeFactoryHelper.Create(_repositoryMock);

        var dispatcher = new JobDispatcherService(
            queue,
            _processingServiceMock.Object,
            scopeFactory.Object,
            OptionsFactory.Create(configure),
            NullLogger<JobDispatcherService>.Instance);

        return (dispatcher, queue);
    }

    [Test]
    public async Task DispatchAvailableJobsAsync_WhenQueueHasJobs_ProcessesEachJob()
    {
        var (dispatcher, queue) = BuildDispatcher();
        foreach (var j in JobFactory.CreateOpenBatch(3)) queue.TryEnqueue(j);

        _processingServiceMock
            .Setup(s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await dispatcher.DispatchAvailableJobsAsync(CancellationToken.None);
        await Task.Delay(200); // allow fire-and-forget tasks to complete

        _processingServiceMock.Verify(
            s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Test]
    public async Task DispatchAvailableJobsAsync_WhenQueueIsEmpty_DoesNotCallProcessingService()
    {
        var (dispatcher, _) = BuildDispatcher();

        await dispatcher.DispatchAvailableJobsAsync(CancellationToken.None);

        _processingServiceMock.Verify(
            s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task DispatchAvailableJobsAsync_OnSuccess_CallsMarkCompletedOnce()
    {
        var (dispatcher, queue) = BuildDispatcher();
        var job = JobFactory.CreateOpen();
        queue.TryEnqueue(job);

        _processingServiceMock
            .Setup(s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await dispatcher.DispatchAvailableJobsAsync(CancellationToken.None);
        await Task.Delay(200);

        _repositoryMock.Verify(
            r => r.MarkCompletedAsync(job.Id, It.IsAny<DateTime>(), CancellationToken.None),
            Times.Once);

        _repositoryMock.Verify(
            r => r.MarkTimedOutAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task DispatchAvailableJobsAsync_OnSuccess_CompletedAtIsRecentUtcTimestamp()
    {
        var (dispatcher, queue) = BuildDispatcher();
        var job  = JobFactory.CreateOpen();
        var before = DateTime.UtcNow;
        queue.TryEnqueue(job);

        _processingServiceMock
            .Setup(s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await dispatcher.DispatchAvailableJobsAsync(CancellationToken.None);
        await Task.Delay(200);

        _repositoryMock.Verify(r =>
            r.MarkCompletedAsync(
                job.Id,
                It.Is<DateTime>(dt => dt >= before && dt <= DateTime.UtcNow),
                CancellationToken.None),
            Times.Once);
    }

    [Test]
    public async Task DispatchAvailableJobsAsync_WhenJobExceedsTimeout_CallsMarkTimedOut()
    {
        var before = DateTime.UtcNow;
        // JobTimeoutMinutes = 0 → CancellationTokenSource fires almost immediately.
        var (dispatcher, queue) = BuildDispatcher(o => o.JobTimeoutMinutes = 0);
        var job = JobFactory.CreateOpen();
        queue.TryEnqueue(job);

        _processingServiceMock
            .Setup(s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Returns(async (Job _, CancellationToken ct) =>
                await Task.Delay(TimeSpan.FromSeconds(5), ct)); // will be cancelled by timeout

        await dispatcher.DispatchAvailableJobsAsync(CancellationToken.None);
        await Task.Delay(500); // give the worker task time to hit the timeout

        _repositoryMock.Verify(r =>
            r.MarkTimedOutAsync(
                job.Id,
                It.Is<DateTime>(dt => dt >= before && dt <= DateTime.UtcNow),
                CancellationToken.None),
            Times.Once);

        _repositoryMock.Verify(
            r => r.MarkCompletedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task DispatchAvailableJobsAsync_WhenHostShutdown_DoesNotMarkCompletedOrTimedOut()
    {
        using var cts           = new CancellationTokenSource();
        var (dispatcher, queue) = BuildDispatcher(o => o.JobTimeoutMinutes = 5);
        var job = JobFactory.CreateOpen();
        queue.TryEnqueue(job);

        _processingServiceMock
            .Setup(s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Returns(async (Job _, CancellationToken ct) =>
                await Task.Delay(TimeSpan.FromSeconds(10), ct));

        await dispatcher.DispatchAvailableJobsAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await Task.Delay(200);

        _repositoryMock.Verify(
            r => r.MarkCompletedAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _repositoryMock.Verify(
            r => r.MarkTimedOutAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task DispatchAvailableJobsAsync_WhenProcessingThrows_DoesNotPropagateException()
    {
        var (dispatcher, queue) = BuildDispatcher();
        queue.TryEnqueue(JobFactory.CreateOpen());

        _processingServiceMock
            .Setup(s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Simulated processing failure"));

        Func<Task> act = () => dispatcher.DispatchAvailableJobsAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task DispatchAvailableJobsAsync_RespectsMaxParallelJobsLimit()
    {
        const int maxParallel = 2;
        const int totalJobs   = 6;

        var (dispatcher, queue) = BuildDispatcher(o =>
        {
            o.MaxParallelJobs   = maxParallel;
            o.JobTimeoutMinutes = 1;
        });

        foreach (var j in JobFactory.CreateOpenBatch(totalJobs)) queue.TryEnqueue(j);

        int concurrent  = 0;
        int maxObserved = 0;
        var lockObj     = new object();

        _processingServiceMock
            .Setup(s => s.ProcessAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Returns(async (Job _, CancellationToken _) =>
            {
                lock (lockObj)
                {
                    concurrent++;
                    if (concurrent > maxObserved) maxObserved = concurrent;
                }
                await Task.Delay(80);
                lock (lockObj) concurrent--;
            });

        await dispatcher.DispatchAvailableJobsAsync(CancellationToken.None);
        await Task.Delay(1000);

        maxObserved.Should().BeLessThanOrEqualTo(maxParallel,
            "the semaphore should never allow more than {0} concurrent workers", maxParallel);
    }
}
