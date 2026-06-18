using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Services;

public interface IJobProcessingService
{
    Task ProcessAsync(Job job, CancellationToken cancellationToken);
}
