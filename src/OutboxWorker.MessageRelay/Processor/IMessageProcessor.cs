namespace OutboxWorker.MessageRelay.Processor;

public interface IMessageProcessor
{
    Task ProcessMessagesAsync(CancellationToken cancellationToken);
    Task InitAsync(CancellationToken cancellationToken);
}