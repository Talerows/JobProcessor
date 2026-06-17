using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Services;

/// <summary>
/// Encapsulates the business logic for processing a single job.
/// </summary>
public interface IJobProcessingService
{
    /// <summary>
    /// Executes the job's work.
    /// The implementation is responsible for simulating work duration and
    /// respecting the provided <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="job">The job to process.</param>
    /// <param name="cancellationToken">
    /// Linked token that is cancelled when either the host shuts down or the job times out.
    /// </param>
    Task ProcessAsync(Job job, CancellationToken cancellationToken);
}
