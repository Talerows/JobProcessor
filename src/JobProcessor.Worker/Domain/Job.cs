namespace JobProcessor.Worker.Domain;

public class Job
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public JobStatus Status { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? TimedOutAt { get; set; }
}
