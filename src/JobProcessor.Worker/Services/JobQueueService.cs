using System.Collections.Concurrent;
using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Services;

/// <summary>
/// Thread-safe, bounded FIFO queue that prevents duplicate job entries.
/// Backed by a <see cref="ConcurrentQueue{T}"/> for the ordering guarantee and a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public class JobQueueService : IJobQueueService
{
    private readonly ConcurrentQueue<Job> _queue = new();
    private readonly ConcurrentDictionary<Guid, byte> _ids = new();
    private readonly int _maxSize;
    private readonly ILogger<JobQueueService> _logger;

    public JobQueueService(int maxSize, ILogger<JobQueueService> logger)
    {
        if (maxSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive.");
        _maxSize = maxSize;
        _logger = logger;
    }

    public int Count => _queue.Count;

    public bool IsFull => _queue.Count >= _maxSize;

    /// <summary>
    /// Attempts to enqueue <paramref name="job"/>.
    /// Silently discards the job if the queue is full or the job is already present.
    /// </summary>
    /// <returns><c>true</c> if the job was successfully enqueued.</returns>
    public bool TryEnqueue(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (_queue.Count >= _maxSize)
        {
            _logger.LogWarning("Queue is full (size={MaxSize}). Job {JobId} was not enqueued.", _maxSize, job.Id);
            return false;
        }

        if (!_ids.TryAdd(job.Id, 0))
        {
            _logger.LogDebug("Job {JobId} is already in the queue. Skipping.", job.Id);
            return false;
        }

        _queue.Enqueue(job);
        _logger.LogDebug("Job {JobId} enqueued. Queue size={QueueSize}.", job.Id, _queue.Count);
        return true;
    }

    /// <summary>
    /// Attempts to dequeue the next job.
    /// </summary>
    /// <returns><c>true</c> and sets <paramref name="job"/> when a job was available.</returns>
    public bool TryDequeue(out Job? job)
    {
        if (_queue.TryDequeue(out job))
        {
            _ids.TryRemove(job.Id, out _);
            return true;
        }

        job = null;
        return false;
    }
}
