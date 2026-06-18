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
        job.Status.Should().Be(JobStatus.Open);
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
        job.CompletedAt.Should().BeNull();
        job.TimedOutAt.Should().BeNull();
    }

    [TestCase(JobStatus.Open)]
    [TestCase(JobStatus.InProgress)]
    [TestCase(JobStatus.Completed)]
    [TestCase(JobStatus.Timeout)]
    public void JobStatus_AllValuesParseableFromString(JobStatus status)
    {
        var parsed = Enum.Parse<JobStatus>(status.ToString());
        parsed.Should().Be(status);
    }

    [Test]
    public void Job_StatusTransition_OpenToInProgress()
    {
        var job = JobFactory.CreateOpen();

        job.Status    = JobStatus.InProgress;
        job.StartedAt = DateTime.UtcNow;

        job.Status.Should().Be(JobStatus.InProgress);
        job.StartedAt.Should().NotBeNull();
    }

    [Test]
    public void Job_StatusTransition_InProgressToCompleted()
    {
        var job = JobFactory.CreateInProgress();

        job.Status      = JobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;

        job.Status.Should().Be(JobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public void Job_StatusTransition_InProgressToTimeout()
    {
        var job = JobFactory.CreateInProgress();

        job.Status     = JobStatus.Timeout;
        job.TimedOutAt = DateTime.UtcNow;

        job.Status.Should().Be(JobStatus.Timeout);
        job.TimedOutAt.Should().NotBeNull();
    }

    [Test]
    public void Job_Id_IsUniquePerInstance()
    {
        var job1 = JobFactory.CreateOpen();
        var job2 = JobFactory.CreateOpen();

        job1.Id.Should().NotBe(job2.Id);
    }

    [Test]
    public void Job_Name_ReflectsProvidedValue()
    {
        var job = JobFactory.CreateOpen(name: "Invoice Processing");
        job.Name.Should().Be("Invoice Processing");
    }
}
