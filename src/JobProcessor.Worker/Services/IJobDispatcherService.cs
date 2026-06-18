namespace JobProcessor.Worker.Services
{
    public interface IJobDispatcherService
    {
        Task DispatchAvailableJobsAsync(CancellationToken cancellationToken);
    }
}