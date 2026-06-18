using FluentAssertions;
using JobProcessor.Worker.Domain;
using JobProcessor.Worker.Tests.Helpers;
using NUnit.Framework;

namespace JobProcessor.Worker.Tests.Domain;

[TestFixture]
public class JobTests
{
    [Test]
    public void Job_DefaultStatus_IsOpen()
    {
        var job = JobFactory.CreateOpen();
        job.Status.Should().Be(OrderStatus.Open);
    }

    [Test]
    public void Job_CreatedAt_IsSetToRecentUtcTime()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var job    = JobFactory.CreateOpen();
        var after  = DateTime.UtcNow.AddSeconds(1);

        job.CreatedAt.Should().BeAfter(before).And.BeBefore(after);
    }

    [Test]
    public void Job_NullableTimestamps_AreNullByDefault()
    {
        var job = JobFactory.CreateOpen();

        job.StartedAt.Should().BeNull();
        job.FinishedAt.Should().BeNull();
        job.TimedOutAt.Should().BeNull();
    }

    [TestCase(OrderStatus.Open)]
    [TestCase(OrderStatus.InProgress)]
    [TestCase(OrderStatus.Completed)]
    [TestCase(OrderStatus.Timeout)]
    public void JobStatus_AllValuesParseableFromString(OrderStatus status)
    {
        var parsed = Enum.Parse<OrderStatus>(status.ToString());
        parsed.Should().Be(status);
    }

    [Test]
    public void Job_StatusTransition_OpenToInProgress()
    {
        var job = JobFactory.CreateOpen();

        job.Status    = OrderStatus.InProgress;
        job.StartedAt = DateTime.UtcNow;

        job.Status.Should().Be(OrderStatus.InProgress);
        job.StartedAt.Should().NotBeNull();
    }

    [Test]
    public void Job_StatusTransition_InProgressToCompleted()
    {
        var job = JobFactory.CreateInProgress();

        job.Status      = OrderStatus.Completed;
        job.FinishedAt = DateTime.UtcNow;

        job.Status.Should().Be(OrderStatus.Completed);
        job.FinishedAt.Should().NotBeNull();
    }

    [Test]
    public void Job_StatusTransition_InProgressToTimeout()
    {
        var job = JobFactory.CreateInProgress();

        job.Status     = OrderStatus.Timeout;
        job.TimedOutAt = DateTime.UtcNow;

        job.Status.Should().Be(OrderStatus.Timeout);
        job.TimedOutAt.Should().NotBeNull();
    }
}
