using JobProcessor.Worker.Configuration;
using JobProcessor.Worker.Infrastructure.Repositories;
using JobProcessor.Worker.Services;
using JobProcessor.Worker.Workers;
using Microsoft.Extensions.Options;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(
                $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<JobProcessorOptions>(
            context.Configuration.GetSection(JobProcessorOptions.SectionName));

        services.AddScoped<IJobRepository>(sp =>
        {
            var opts   = sp.GetRequiredService<IOptions<JobProcessorOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<PostgresJobRepository>>();
            var connStr = context.Configuration.GetConnectionString(opts.ConnectionStringName)
                          ?? throw new InvalidOperationException(
                              $"Connection string '{opts.ConnectionStringName}' is not configured.");
            return new PostgresJobRepository(connStr, logger);
        });

        services.AddSingleton<IJobQueueService>(sp =>
        {
            var opts   = sp.GetRequiredService<IOptions<JobProcessorOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<JobQueueService>>();
            return new JobQueueService(opts.MaxQueueSize, logger);
        });

        services.AddSingleton<IJobProcessingService, JobProcessingService>();
        services.AddSingleton<IJobDispatcherService, JobDispatcherService>();
        services.AddSingleton<IJobFetcherService, JobFetcherService>();
        services.AddHostedService<JobProcessorWorker>();
    })
    .Build();

await host.RunAsync();
