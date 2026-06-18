namespace JobProcessor.Worker.Domain;

public class Order
{
    public long Id { get; init; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? TimedOutAt { get; set; }
}
