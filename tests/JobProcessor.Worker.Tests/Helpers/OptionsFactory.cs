using JobProcessor.Worker.Configuration;
using Microsoft.Extensions.Options;

namespace JobProcessor.Worker.Tests.Helpers;

public static class OptionsFactory
{

    public static IOptions<JobProcessorOptions> Create(
        Action<JobProcessorOptions>? configure = null)
    {
        var opts = new JobProcessorOptions
        {
            PollingIntervalSeconds = 5,
            MaxQueueSize           = 10,
            MaxParallelJobs        = 3,
            JobTimeoutMinutes      = 1,
            ConnectionStringName   = "JobProcessor",
        };

        configure?.Invoke(opts);
        return Options.Create(opts);
    }
}
