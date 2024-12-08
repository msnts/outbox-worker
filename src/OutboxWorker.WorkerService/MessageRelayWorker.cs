using System.Diagnostics;
using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OutboxWorker.WorkerService.Configurations;
using OutboxWorker.WorkerService.Extensions;

namespace OutboxWorker.WorkerService;

public class MessageRelayWorker : BackgroundService
{
    private readonly IOptions<OutboxOptions> _options;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<MessageRelayWorker> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSender _sender;
    private readonly IMongoClient _mongoClient;
    private readonly IMongoCollection<RawBsonDocument> _outboxMessages;
    private readonly FindOptions<RawBsonDocument> _findOptions;
    private readonly CreateMessageBatchOptions _batchOptions;
    private readonly OutboxMetrics _metrics;
    private readonly ParallelOptions _parallelOptions;
    private readonly DeleteOptions _deleteOptions;
    private IClientSessionHandle _currentSession;
    private readonly int _sliceSize;

    public MessageRelayWorker(IOptions<OutboxOptions> options, IMongoClient mongoClient, ServiceBusClient busClient,
        ActivitySource activitySource, ILogger<MessageRelayWorker> logger, OutboxMetrics metrics)
    {
        _options = options;
        _logger = logger;
        _activitySource = activitySource;
        _mongoClient = mongoClient;
        _serviceBusClient = busClient;
        _metrics = metrics;
        
        _sender = _serviceBusClient.CreateSender(_options.Value.BrokerOptions.EntityName);
        
        _outboxMessages = _mongoClient.GetCollection(_options.Value.MongoOptions.DatabaseName, "OutboxMessage");
        
        _findOptions = new FindOptions<RawBsonDocument>
        {
            Sort = Builders<RawBsonDocument>.Sort.Ascending(m => m["_id"]),
            Limit = _options.Value.MongoOptions.Limit,
            BatchSize = _options.Value.MongoOptions.BatchSize
        };
        
        _batchOptions = new CreateMessageBatchOptions()
        {
            MaxSizeInBytes = 262144
        };

        _parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = _options.Value.MaxDegreeOfParallelism
        };

        _deleteOptions = new DeleteOptions();
        
        _sliceSize = _options.Value.SliceSize;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var messageBatch = await _sender.CreateMessageBatchAsync(_batchOptions, stoppingToken);
        var stopwatch = new Stopwatch();
        _parallelOptions.CancellationToken = stoppingToken;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            stopwatch.Start();
            
            using var activity = _activitySource.StartActivity();
            
            try
            {
                //todo: acquire lock

                await DoExecuteAsync(stoppingToken);
            }
            //todo: tratar as exceptions
            finally
            {
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds < _options.Value.Delay)
                {
                    await Task.Delay(_options.Value.Delay - (int)stopwatch.ElapsedMilliseconds, stoppingToken);
                }
                
                stopwatch.Reset();
            }
            
            break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task DoExecuteAsync(CancellationToken stoppingToken)
    {
        using var session = await _mongoClient.StartSessionAsync(cancellationToken: stoppingToken);
        session.StartTransaction();
        _currentSession = session;

        try
        {
            using var cursor = await _outboxMessages.FindAsync(session, x => true, _findOptions, stoppingToken);
            
            var messages = await cursor.ToListAsync(stoppingToken);
            
            var slices = messages.SliceInMemory(_sliceSize);

            await Parallel.ForEachAsync(slices, stoppingToken, async (memory, token) => await ProcessSliceAsync(memory, token));
        }
        finally
        {
            await session.AbortTransactionAsync(stoppingToken);
            //await session.CommitTransactionAsync(stoppingToken);
        }
    }

    private async Task ProcessSliceAsync(ReadOnlyMemory<RawBsonDocument> messages, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity();
        var count = 0;
        var t = 0u;
        int i;
        var taskCount = (messages.Length + _options.Value.BrokerOptions.BatchSize - 1) / _options.Value.BrokerOptions.BatchSize;
        var tasks = new Task[taskCount];
        var batchSize = _options.Value.BrokerOptions.BatchSize;
        
        var messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
        
        for (i = 0; i < messages.Length; i++)
        {
            var message = messages.Span[i].ToServiceBusMessage();
            
            if (count < batchSize && messageBatch.TryAddMessage(message))
            {
                count++;
                continue;
            }

            tasks[t++] = SendMessageBatchAsync(messageBatch, stoppingToken);
            //tasks[t++] = ProcessMessageBatchAsync(messageBatch, messages.Span[i - count], messages.Span[i], stoppingToken);
            messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
            count = 0;
        }

        i--;
        tasks[t] = SendMessageBatchAsync(messageBatch, stoppingToken);
        //tasks[t] = ProcessMessageBatchAsync(messageBatch, messages.Span[i - count], messages.Span[i], stoppingToken);

        await Task.WhenAll(tasks);
    }
    
    private async Task SendMessageBatchAsync(ServiceBusMessageBatch messageBatch, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity();
        //await _sender.SendMessagesAsync(messageBatch, stoppingToken);
        await Task.Delay(300, stoppingToken);
        messageBatch.Dispose();
        _metrics.IncrementMessageCount(messageBatch.Count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task RemoveMessageBatchAsync(RawBsonDocument firstMessage, RawBsonDocument lastMessage, CancellationToken stoppingToken)
    {
        var first = firstMessage["_id"].AsGuid.ToString();
        var last = lastMessage["_id"].AsGuid.ToString();
        
        var builder = Builders<RawBsonDocument>.Filter;
        //Todo: Remover essa conversão de string para BsonValue
        var filter = builder.And(builder.Gte(r => r["_id"], first), builder.Lte(r => r["_id"], last));

        await _outboxMessages.DeleteManyAsync(_currentSession, filter, _deleteOptions, stoppingToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task ProcessMessageBatchAsync(ServiceBusMessageBatch messageBatch, RawBsonDocument firstMessage, RawBsonDocument lastMessage, CancellationToken stoppingToken)
    {
        await SendMessageBatchAsync(messageBatch, stoppingToken);
        await RemoveMessageBatchAsync(firstMessage, lastMessage, stoppingToken);
    }
}