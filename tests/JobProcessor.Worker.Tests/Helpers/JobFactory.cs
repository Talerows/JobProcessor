using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Tests.Helpers;

public static class JobFactory
{
    public static Order CreateOpen(long? id = null) => new()
    {
        Id        = id ?? 1,
        Status    = OrderStatus.Open,
        CreatedAt = DateTime.UtcNow,
    };

    public static Order CreateInProgress(long? id = null) => new()
    {
        Id        = id ?? 1,
        Status    = OrderStatus.InProgress,
        CreatedAt = DateTime.UtcNow.AddMinutes(-1),
        StartedAt = DateTime.UtcNow,
    };

    public static IReadOnlyList<Order> CreateOpenBatch(int count) =>
        Enumerable.Range(1, count)
                  .Select(i => CreateOpen(id:i))
                  .ToList();
}
