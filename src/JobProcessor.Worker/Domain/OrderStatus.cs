namespace JobProcessor.Worker.Domain;

public enum OrderStatus
{
    Open,
    InProgress,
    Completed,
    Timeout
}
