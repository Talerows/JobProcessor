using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Services;

public interface IJobProcessingService
{
    Task ProcessAsync(Order job, CancellationToken cancellationToken);
}
