using DistributedLock.Mongo;
using MongoDB.Bson;

namespace OutboxWorker.WorkerService;

public interface IMessageRepository
{
    Task StartTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task AbortTransactionAsync(CancellationToken stoppingToken);
    Task<List<RawBsonDocument>> FindMessagesAsync(CancellationToken cancellationToken);
    Task RemoveMessagesAsync(RawBsonDocument firstMessage, RawBsonDocument lastMessage, CancellationToken cancellationToken);
    Task<IAcquire> AcquireLockAsync(CancellationToken stoppingToken);
}