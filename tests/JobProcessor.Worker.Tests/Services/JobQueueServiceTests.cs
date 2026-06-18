using FluentAssertions;
using JobProcessor.Worker.Services;
using JobProcessor.Worker.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace JobProcessor.Worker.Tests.Services;

[TestFixture]
public sealed class JobQueueServiceTests
{
    [Test]
    public void Constructor_WhenMaxSizeIsZero_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new JobQueueService(0, NullLogger<JobQueueService>.Instance);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("maxSize");
    }

    [Test]
    public void Constructor_WhenMaxSizeIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new JobQueueService(-1, NullLogger<JobQueueService>.Instance);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("maxSize");
    }

    [Test]
    public void TryEnqueue_WhenQueueIsEmpty_ReturnsTrueAndIncreasesCount()
    {
        var queue = BuildQueue(maxSize: 5);
        var job   = JobFactory.CreateOpen();

        var result = queue.TryEnqueue(job);

        result.Should().BeTrue();
        queue.Count.Should().Be(1);
    }

    [Test]
    public void TryEnqueue_MultipleDistinctJobs_AllEnqueued()
    {
        var queue = BuildQueue(maxSize: 5);

        foreach (var job in JobFactory.CreateOpenBatch(3))
            queue.TryEnqueue(job).Should().BeTrue();

        queue.Count.Should().Be(3);
    }

    [Test]
    public void TryEnqueue_JobWithSameId_ReturnsFalseAndCountUnchanged()
    {
        var queue = BuildQueue(maxSize: 5);
        var id    = Guid.NewGuid();

        queue.TryEnqueue(JobFactory.CreateOpen(id: id)).Should().BeTrue();
        queue.TryEnqueue(JobFactory.CreateOpen(id: id)).Should().BeFalse(); // same ID, new instance

        queue.Count.Should().Be(1);
    }

    [Test]
    public void TryEnqueue_WhenFull_ReturnsFalse()
    {
        var queue = BuildQueue(maxSize: 2);
        queue.TryEnqueue(JobFactory.CreateOpen());
        queue.TryEnqueue(JobFactory.CreateOpen());

        var result = queue.TryEnqueue(JobFactory.CreateOpen());

        result.Should().BeFalse();
        queue.Count.Should().Be(2);
    }

    [Test]
    public void IsFull_WhenAtCapacity_ReturnsTrue()
    {
        var queue = BuildQueue(maxSize: 1);
        queue.TryEnqueue(JobFactory.CreateOpen());

        queue.IsFull.Should().BeTrue();
    }

    [Test]
    public void IsFull_WhenBelowCapacity_ReturnsFalse()
    {
        var queue = BuildQueue(maxSize: 5);
        queue.TryEnqueue(JobFactory.CreateOpen());

        queue.IsFull.Should().BeFalse();
    }

    [Test]
    public void TryEnqueue_NullJob_ThrowsArgumentNullException()
    {
        var queue = BuildQueue(maxSize: 5);

        Action act = () => queue.TryEnqueue(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void TryDequeue_WhenEmpty_ReturnsFalseAndNullJob()
    {
        var queue  = BuildQueue(maxSize: 5);

        var result = queue.TryDequeue(out var job);

        result.Should().BeFalse();
        job.Should().BeNull();
    }

    [Test]
    public void TryDequeue_AfterEnqueue_ReturnsTrueWithCorrectJob()
    {
        var queue = BuildQueue(maxSize: 5);
        var job   = JobFactory.CreateOpen();
        queue.TryEnqueue(job);

        var result = queue.TryDequeue(out var dequeued);

        result.Should().BeTrue();
        dequeued.Should().BeSameAs(job);
        queue.Count.Should().Be(0);
    }

    [Test]
    public void TryDequeue_RemovesIdFromDuplicateGuard_AllowsReEnqueue()
    {
        var queue = BuildQueue(maxSize: 5);
        var job   = JobFactory.CreateOpen();

        queue.TryEnqueue(job);
        queue.TryDequeue(out _);

        // After dequeue the ID slot is free again.
        queue.TryEnqueue(job).Should().BeTrue();
    }

    [Test]
    public void TryDequeue_RespectsInsertionOrder()
    {
        var queue = BuildQueue(maxSize: 5);
        var jobs  = JobFactory.CreateOpenBatch(3).ToList();

        foreach (var job in jobs)
            queue.TryEnqueue(job);

        for (int i = 0; i < jobs.Count; i++)
        {
            queue.TryDequeue(out var dequeued);
            dequeued!.Id.Should().Be(jobs[i].Id, $"position {i} should match insertion order");
        }
    }

    [Test]
    public async Task TryEnqueue_ConcurrentAccess_NoDuplicatesAndCountIsCorrect()
    {
        const int jobCount = 100;
        var queue = BuildQueue(maxSize: jobCount);
        var jobs  = JobFactory.CreateOpenBatch(jobCount).ToList();

        await Parallel.ForEachAsync(jobs, async (job, _) =>
        {
            await Task.Yield();
            queue.TryEnqueue(job);
        });

        queue.Count.Should().Be(jobCount);

        var seen = new HashSet<Guid>();
        while (queue.TryDequeue(out var job))
            seen.Add(job!.Id).Should().BeTrue("each job ID must be unique");

        seen.Should().HaveCount(jobCount);
    }

    private static JobQueueService BuildQueue(int maxSize) =>
        new(maxSize, NullLogger<JobQueueService>.Instance);
}
