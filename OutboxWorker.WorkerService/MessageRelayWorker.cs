using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

    public MessageRelayWorker(IOptions<OutboxOptions> options, IMongoClient mongoClient, ServiceBusClient busClient,
        ActivitySource activitySource, ILogger<MessageRelayWorker> logger, OutboxMetrics metrics)
    {
        _options = options;
        _logger = logger;
        _activitySource = activitySource;
        _mongoClient = mongoClient;
        _serviceBusClient = busClient;
        _metrics = metrics;
        
        _sender = _serviceBusClient.CreateSender("messages");
        
        var database = _mongoClient.GetDatabase("OutboxWorkerService");

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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>((_options.Value.MongoOptions.Limit / _options.Value.BatchSize) + 1);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var messageBatch = await _sender.CreateMessageBatchAsync(_batchOptions, stoppingToken);
            try
            {
                using var activity = _activitySource.StartActivity();

                using var session = await _mongoClient.StartSessionAsync(cancellationToken: stoppingToken);
                session.StartTransaction();

                using (var cursor = await _outboxMessages.FindAsync(session, x => true, _findOptions, stoppingToken))
                {
                    while (await cursor.MoveNextAsync(stoppingToken))
                    {
                        //todo: Paralelizar o processamento com Parallel.ForEachAsync()
                        tasks.Add(ProcessCursorBatchAsync(cursor.Current.ToList().AsMemory(), stoppingToken));
                    }
                }

                await Task.WhenAll(tasks);
                
                await session.CommitTransactionAsync(stoppingToken);
                
                tasks.Clear();
            }
            finally
            {
                await Task.Delay(_options.Value.Delay, stoppingToken);
            }
            
            break;
        }
    }

    private async Task ProcessCursorBatchAsync(ReadOnlyMemory<RawBsonDocument> messages, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity();
        var count = 0u;
        var tasks = new List<Task>((messages.Length / _options.Value.BatchSize) + 1);
        var messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
        
        for (var i = 0; i < messages.Length; i++)
        {
            var message = OutboxMessageToServiceBusMessage(messages.Span[i]);
            
            if (messageBatch.TryAddMessage(message) && count < _options.Value.BatchSize)
            {
                count++;
                continue;
            }

            tasks.Add(SendMessageBatchAsync(messageBatch, stoppingToken));
            messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
            count = 0;
        }
        
        tasks.Add(SendMessageBatchAsync(messageBatch, stoppingToken));

        await Task.WhenAll(tasks);
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
        await Task.Delay(800, stoppingToken);
        messageBatch.Dispose();
        _metrics.IncrementMessageCount(messageBatch.Count);
    }
}