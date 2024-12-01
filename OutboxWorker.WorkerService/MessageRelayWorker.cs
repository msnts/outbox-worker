using System.Diagnostics;
using System.Runtime.CompilerServices;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OutboxWorker.WorkerService.Configurations;

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
        
        var database = _mongoClient.GetDatabase(_options.Value.MongoOptions.DatabaseName);

        _outboxMessages = database.GetCollection<RawBsonDocument>("OutboxMessage");
        
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

        try
        {
            using var cursor = await _outboxMessages.FindAsync(session, x => true, _findOptions, stoppingToken);
                    
            var slices = Slices(cursor.ToList().AsMemory());

            await Parallel.ForEachAsync(slices, stoppingToken, async (memory, token) => await ProcessCursorBatchAsync(session, memory, token));
        }
        finally
        {
            await session.CommitTransactionAsync(stoppingToken);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlyMemory<RawBsonDocument>[] Slices(ReadOnlyMemory<RawBsonDocument> messages)
    {
        var sliceSize = _options.Value.MongoOptions.Limit / _options.Value.MaxDegreeOfParallelism;
        var sliceCount = (messages.Length + sliceSize - 1) / sliceSize;
        int start;
        var slices = new ReadOnlyMemory<RawBsonDocument>[sliceCount];

        for (var i = 0; i < sliceCount; i++)
        {
            start = i * sliceSize;
            if (start + sliceSize > messages.Length)
            {
                slices[i] = messages.Slice(start);
                break;
            }
            slices[i] = messages.Slice(start, sliceSize);
        }

        return slices;
    }

    private async Task ProcessCursorBatchAsync(IClientSessionHandle session, ReadOnlyMemory<RawBsonDocument> messages, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity();
        var count = 0u;
        var t = 0u;
        var taskCount = (messages.Length + _options.Value.BrokerOptions.BatchSize - 1) / _options.Value.BrokerOptions.BatchSize;
        var tasks = new Task[taskCount];
        var batchSize = _options.Value.BrokerOptions.BatchSize;
        
        var messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
        
        for (var i = 0; i < messages.Length; i++)
        {
            var message = OutboxMessageToServiceBusMessage(messages.Span[i]);
            
            if (count < batchSize && messageBatch.TryAddMessage(message))
            {
                count++;
                continue;
            }

            tasks[t++] = SendMessageBatchAsync(messageBatch, stoppingToken);
            messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
            count = 0;
        }
        
        tasks[t] = SendMessageBatchAsync(messageBatch, stoppingToken);

        await Task.WhenAll(tasks);
        
        //todo: excluir as mensagens antes do commit
        /*var first = messages.Span[0]["_id"].AsGuid.ToString();
        var last = messages.Span[messages.Length - 1]["_id"].AsGuid.ToString();
        
        var builder = Builders<RawBsonDocument>.Filter;
        var filter = builder.And(builder.Gte(r => r["_id"], first), builder.Lte(r => r["_id"], last));

        await _outboxMessages.DeleteManyAsync(session, filter, stoppingToken);*/
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ServiceBusMessage OutboxMessageToServiceBusMessage(RawBsonDocument message)
    {
        return new ServiceBusMessage(message["Body"].AsBsonDocument.ToJson())
        {
            MessageId = message["_id"].AsGuid.ToString(),
            CorrelationId = message["CorrelationId"].AsGuid.ToString(),
            Subject = message["Subject"].AsString,
            ContentType = "application/json"
        };
    }
    
    private async Task SendMessageBatchAsync(ServiceBusMessageBatch messageBatch, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity();
        //await _sender.SendMessagesAsync(messageBatch, stoppingToken);
        await Task.Delay(600, stoppingToken);
        messageBatch.Dispose();
        _metrics.IncrementMessageCount(messageBatch.Count);
    }
}