using JobProcessor.Worker.Domain;

namespace JobProcessor.Worker.Services
{
    public interface IJobQueueService
    {
        int Count { get; }
        bool IsFull { get; }

        bool TryDequeue(out Order? job);
        bool TryEnqueue(Order job);
    }
}