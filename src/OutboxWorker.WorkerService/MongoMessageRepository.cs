using MongoDB.Bson;
using MongoDB.Driver;
using OutboxWorker.WorkerService.Configurations;

namespace OutboxWorker.WorkerService;

public class MongoMessageRepository : IMessageRepository
{
    private readonly IMongoCollection<RawBsonDocument> _outboxMessages;
    private readonly FindOptions<RawBsonDocument> _findOptions;
    private readonly DeleteOptions _deleteOptions;

    public MongoMessageRepository(IMongoCollection<RawBsonDocument> outboxMessages, OutboxOptions options)
    {
        _outboxMessages = outboxMessages;
        _findOptions = CreateFindOptions(options);
        _deleteOptions = new DeleteOptions();
    }

    public async Task<IEnumerable<RawBsonDocument>> GetMessagesAsync(
        IClientSessionHandle session, 
        CancellationToken cancellationToken)
    {
        var cursor = await _outboxMessages.FindAsync(session, x => true, _findOptions, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task RemoveMessagesAsync(
        IClientSessionHandle session,
        RawBsonDocument firstMessage,
        RawBsonDocument lastMessage,
        CancellationToken cancellationToken)
    {
        var filter = CreateDeleteFilter(firstMessage, lastMessage);
        await _outboxMessages.DeleteManyAsync(session, filter, _deleteOptions, cancellationToken);
    }

    private static FindOptions<RawBsonDocument> CreateFindOptions(OutboxOptions options) =>
        new()
        {
            Sort = Builders<RawBsonDocument>.Sort.Ascending(m => m["_id"]),
            Limit = options.MongoOptions.Limit,
            BatchSize = options.MongoOptions.BatchSize
        };

    private static FilterDefinition<RawBsonDocument> CreateDeleteFilter(
        RawBsonDocument firstMessage,
        RawBsonDocument lastMessage)
    {
        var builder = Builders<RawBsonDocument>.Filter;
        var first = firstMessage["_id"].AsGuid.ToString();
        var last = lastMessage["_id"].AsGuid.ToString();
        return builder.And(builder.Gte(r => r["_id"], first), builder.Lte(r => r["_id"], last));
    }
}