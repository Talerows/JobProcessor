namespace JobProcessor.Worker.Domain;

public enum JobStatus
{
    Open,
    InProgress,
    Completed,
    Timeout
}
