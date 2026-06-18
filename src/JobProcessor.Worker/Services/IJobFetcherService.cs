namespace JobProcessor.Worker.Services
{
    public interface IJobFetcherService
    {
        Task FetchAndEnqueueAsync(CancellationToken cancellationToken);
    }
}