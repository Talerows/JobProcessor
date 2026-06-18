using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Tests.Helpers;

public static class JobFactory
{
    public static Job CreateOpen(Guid? id = null, string name = "Test Job") => new()
    {
        Id        = id ?? Guid.NewGuid(),
        Name      = name,
        Status    = JobStatus.Open,
        CreatedAt = DateTime.UtcNow,
    };

    public static Job CreateInProgress(Guid? id = null, string name = "Test Job") => new()
    {
        Id        = id ?? Guid.NewGuid(),
        Name      = name,
        Status    = JobStatus.InProgress,
        CreatedAt = DateTime.UtcNow.AddMinutes(-1),
        StartedAt = DateTime.UtcNow,
    };

    public static IReadOnlyList<Job> CreateOpenBatch(int count) =>
        Enumerable.Range(1, count)
                  .Select(i => CreateOpen(name: $"Job {i}"))
                  .ToList();
}
