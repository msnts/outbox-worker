using DistributedLock.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OutboxWorker.WorkerService.Configurations;

namespace OutboxWorker.WorkerService;

public class MessageRepository : IMessageRepository
{
    private readonly IMongoClient _mongoClient;
    private readonly IMongoCollection<RawBsonDocument> _outboxMessages;
    private readonly MongoLock<Guid> _mongoLock;
    private readonly FindOptions<RawBsonDocument> _findOptions;
    private readonly DeleteOptions _deleteOptions;
    private IClientSessionHandle? _currentSession;

    public MessageRepository(IMongoClient mongoClient, IOptions<OutboxOptions> options)
    {
        _mongoClient = mongoClient;
        _findOptions = CreateFindOptions(options.Value);
        _deleteOptions = new DeleteOptions();
        var database = _mongoClient.GetDatabase(options.Value.MongoOptions.DatabaseName);
        _outboxMessages = database.GetCollection<RawBsonDocument>("OutboxMessage");;
        var locks = database.GetCollection<LockAcquire<Guid>>("locks");
        var signals = database.GetCollection<ReleaseSignal>("signals");
        
        var lockId = Guid.Parse("BF431614-4FB0-4489-84AA-D3EFEEF6BE7E");

        _mongoLock = new MongoLock<Guid>(locks, signals, lockId);
    }

    public async Task StartTransactionAsync(CancellationToken cancellationToken)
    {
        _currentSession = await _mongoClient.StartSessionAsync(cancellationToken: cancellationToken);
        _currentSession.StartTransaction();
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (_currentSession?.IsInTransaction is false)
        {
            throw new Exception();
        }
        await _currentSession!.CommitTransactionAsync(cancellationToken);
        _currentSession.Dispose();
        _currentSession = null;
    }

    public async Task AbortTransactionAsync(CancellationToken cancellationToken)
    {
        if (_currentSession?.IsInTransaction is false)
        {
            throw new Exception();
        }
        await _currentSession!.AbortTransactionAsync(cancellationToken);
        _currentSession.Dispose();
        _currentSession = null;
    }
    
    public async Task<List<RawBsonDocument>> FindMessagesAsync(CancellationToken cancellationToken)
    {
        var cursor = await _outboxMessages.FindAsync(_currentSession, x => true, _findOptions, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task RemoveMessagesAsync(RawBsonDocument firstMessage, RawBsonDocument lastMessage, CancellationToken cancellationToken)
    {
        var filter = CreateDeleteFilter(firstMessage, lastMessage);
        await _outboxMessages.DeleteManyAsync(_currentSession, filter, _deleteOptions, cancellationToken);
    }

    public Task<IAcquire> AcquireLockAsync(CancellationToken stoppingToken)
    {
        return _mongoLock.AcquireAsync(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
    }

    private static FindOptions<RawBsonDocument> CreateFindOptions(OutboxOptions options) => new()
    {
        Sort = Builders<RawBsonDocument>.Sort.Ascending(m => m["_id"]),
        Limit = options.MongoOptions.Limit,
        BatchSize = options.MongoOptions.BatchSize
    };

    private static FilterDefinition<RawBsonDocument> CreateDeleteFilter(RawBsonDocument firstMessage, RawBsonDocument lastMessage)
    {
        //Todo: Utilizar cache para para esse filter
        var builder = Builders<RawBsonDocument>.Filter;
        var first = firstMessage["_id"].AsGuid.ToString();
        var last = lastMessage["_id"].AsGuid.ToString();
        //Todo: Remover essa conversão de string para BsonValue
        return builder.And(builder.Gte(r => r["_id"], first), builder.Lte(r => r["_id"], last));
    }
}