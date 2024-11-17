using System.Diagnostics;
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
    private readonly IMongoCollection<OutboxMessage> _outboxMessages;

    public MessageRelayWorker(IOptions<OutboxOptions> options, IMongoClient mongoClient, ServiceBusClient busClient,
        ActivitySource activitySource, ILogger<MessageRelayWorker> logger)
    {
        _options = options;
        _logger = logger;
        _activitySource = activitySource;
        _mongoClient = mongoClient;
        _serviceBusClient = busClient;
        
        _sender = _serviceBusClient.CreateSender("messages");
        
        var database = _mongoClient.GetDatabase("OutboxWorkerService");

        _outboxMessages = database.GetCollection<OutboxMessage>("OutboxMessage");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>((_options.Value.MongoOptions.Limit / _options.Value.BatchSize) + 1);
        
        var findOptions = new FindOptions<OutboxMessage>
        {
            Sort = Builders<OutboxMessage>.Sort.Ascending(m => m.Id),
            Limit = _options.Value.MongoOptions.Limit,
            BatchSize = _options.Value.MongoOptions.BatchSize
        };
        var batchOptions = new CreateMessageBatchOptions()
        {
            MaxSizeInBytes = 262144
        };
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var messageBatch = await _sender.CreateMessageBatchAsync(batchOptions, stoppingToken);
            try
            {
                using var activity = _activitySource.StartActivity("Sender");

                using var session = await _mongoClient.StartSessionAsync(cancellationToken: stoppingToken);
                session.StartTransaction();

                using (var cursor = await _outboxMessages.FindAsync(session, x => true, findOptions, stoppingToken))
                {
                    while (await cursor.MoveNextAsync(stoppingToken))
                    {
                        tasks.Add(ProcessCursorBatchAsync(cursor.Current.ToList(), stoppingToken));
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

    private async Task ProcessCursorBatchAsync(List<OutboxMessage> messages, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity();
        var count = 0;
        var tasks = new List<Task>((messages.Count / _options.Value.BatchSize) + 1);
        var messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
        
        for (var i = 0; i < messages.Count; i++)
        {
            var message = OutboxMessageToServiceBusMessage(messages[i]);
            
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
    
    private static ServiceBusMessage OutboxMessageToServiceBusMessage(OutboxMessage message)
    {
        return new ServiceBusMessage(message.ToJson())
        {
            MessageId = message.Id.ToString(),
            CorrelationId = message.CorrelationId.ToString(),
            Subject = message.Subject,
            ContentType = "application/json"
        };
    }

    private async Task SendMessageBatchAsync(ServiceBusMessageBatch messageBatch, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity();
        //await _sender.SendMessagesAsync(messageBatch, stoppingToken);
        await Task.Delay(800, stoppingToken);
        messageBatch.Dispose();
    }
}