using FluentAssertions;
using JobProcessor.Worker.Configuration;
using JobProcessor.Worker.Tests.Helpers;
using NUnit.Framework;

namespace JobProcessor.Worker.Tests.Configuration;

[TestFixture]
public class JobProcessorOptionsTests
{
    [Test]
    public void DefaultOptions_HaveExpectedValues()
    {
        var opts = new JobProcessorOptions();

        opts.PollingIntervalSeconds.Should().Be(10);
        opts.MaxQueueSize.Should().Be(50);
        opts.MaxParallelJobs.Should().Be(5);
        opts.JobTimeoutMinutes.Should().Be(5);
        opts.ConnectionStringName.Should().Be("JobProcessor");
    }

    [Test]
    public void OptionsFactory_WithOverrides_AppliesCorrectly()
    {
        var opts = OptionsFactory.Create(o =>
        {
            o.MaxParallelJobs   = 10;
            o.JobTimeoutMinutes = 2;
        });

        opts.Value.MaxParallelJobs.Should().Be(10);
        opts.Value.JobTimeoutMinutes.Should().Be(2);
    }
}
