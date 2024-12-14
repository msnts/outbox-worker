namespace OutboxWorker.WorkerService;

public interface IMessageProcessor
{
    Task ProcessMessagesAsync(CancellationToken cancellationToken);
    Task InitAsync(CancellationToken cancellationToken);
}