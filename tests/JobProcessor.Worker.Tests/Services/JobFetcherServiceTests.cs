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
public sealed class JobFetcherServiceTests
{
    private Mock<IJobRepository> _repositoryMock = null!;
    private JobQueueService _queue = null!;
    private const int _maxQueueSize = 10;

    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<IJobRepository>();
        _queue = new JobQueueService(_maxQueueSize, NullLogger<JobQueueService>.Instance);
    }

    private JobFetcherService BuildFetcher(Action<JobProcessorOptions>? configure = null)
    {
        var scopeFactory = ScopeFactoryHelper.Create(_repositoryMock);
        return new JobFetcherService(
            scopeFactory.Object,
            _queue,
            OptionsFactory.Create(configure),
            NullLogger<JobFetcherService>.Instance);
    }

    [Test]
    public async Task FetchAndEnqueueAsync_WhenJobsReturned_EnqueuesAllJobs()
    {
        var jobs = JobFactory.CreateOpenBatch(3);
        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        await BuildFetcher().FetchAndEnqueueAsync(CancellationToken.None);

        _queue.Count.Should().Be(3);
    }

    [Test]
    public async Task FetchAndEnqueueAsync_WhenNoJobsReturned_QueueRemainsEmpty()
    {
        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Job>());

        await BuildFetcher().FetchAndEnqueueAsync(CancellationToken.None);

        _queue.Count.Should().Be(0);
    }

    [Test]
    public async Task FetchAndEnqueueAsync_RequestsBatchSizeEqualToAvailableSlots()
    {
        // Pre-fill 4 jobs -> 10 - 4 = 6 available slots.
        foreach (var j in JobFactory.CreateOpenBatch(4))
            _queue.TryEnqueue(j);

        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Job>());

        await BuildFetcher(o => o.MaxQueueSize = 10).FetchAndEnqueueAsync(CancellationToken.None);

        _repositoryMock.Verify(
            r => r.ClaimOpenJobsAsync(6, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task FetchAndEnqueueAsync_WhenQueueIsFull_DoesNotCallRepository()
    {
        foreach (var j in JobFactory.CreateOpenBatch(_maxQueueSize))
            _queue.TryEnqueue(j);

        await BuildFetcher(o => o.MaxQueueSize = _maxQueueSize).FetchAndEnqueueAsync(CancellationToken.None);

        _repositoryMock.Verify(
            r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task FetchAndEnqueueAsync_WhenRepositoryThrows_DoesNotThrowAndQueueRemainsEmpty()
    {
        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        Func<Task> act = () => BuildFetcher().FetchAndEnqueueAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        _queue.Count.Should().Be(0);
    }

    [Test]
    public async Task FetchAndEnqueueAsync_WhenCancelledDuringDbCall_DoesNotThrow()
    {
        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        Func<Task> act = () => BuildFetcher().FetchAndEnqueueAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task FetchAndEnqueueAsync_CallsRepositoryExactlyOnce()
    {
        _repositoryMock
            .Setup(r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Job>());

        await BuildFetcher().FetchAndEnqueueAsync(CancellationToken.None);

        _repositoryMock.Verify(
            r => r.ClaimOpenJobsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
