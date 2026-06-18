using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Infrastructure.Repositories;

public interface IJobRepository
{
    /// </summary>
    /// <param name="batchSize">Maximum number of jobs to claim in one call.</param>
    /// <returns>The jobs that were successfully claimed by this instance.</returns>
    Task<IReadOnlyList<Order>> ClaimOpenJobsAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a job as successfully completed and records the completion timestamp.
    /// </summary>
    Task MarkCompletedAsync(long jobId, DateTime completedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a job as timed out and records the timeout timestamp.
    /// </summary>
    Task MarkTimedOutAsync(long jobId, DateTime timedOutAt, CancellationToken cancellationToken);
}
