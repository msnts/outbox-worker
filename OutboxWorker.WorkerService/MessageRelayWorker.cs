using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OutboxWorker.WorkerService.Configurations;
using WorkerService1;

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
        var tasks = new List<Task>(_options.Value.MongoOptions.BatchSize);
        
        var findOptions = new FindOptions<OutboxMessage>
        {
            Sort = Builders<OutboxMessage>.Sort.Ascending(m => m.Id),
            Limit = _options.Value.MongoOptions.Limit,
            BatchSize = _options.Value.MongoOptions.BatchSize
        };
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var activity = _activitySource.StartActivity("Sender");
                
                using (var session = await _mongoClient.StartSessionAsync(cancellationToken: stoppingToken))
                {
                    session.StartTransaction();
                    
                    using (var cursor = await _outboxMessages.FindAsync(session, x => true, findOptions, stoppingToken))
                    {
                        activity?.AddEvent(new("Processing start"));
                        var messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
                        var count = 0;
                        
                        while (await cursor.MoveNextAsync(stoppingToken))
                        {
                            foreach (var message in cursor.Current)
                            {
                                if (messageBatch.TryAddMessage(new ServiceBusMessage(message.Body)) && count < _options.Value.BatchSize)
                                {
                                    count++;
                                    continue;
                                }

                                tasks.Add(SendMessageBatch(messageBatch, stoppingToken));
                                messageBatch = await _sender.CreateMessageBatchAsync(stoppingToken);
                                count = 0;
                            }
                        }
                        
                        tasks.Add(SendMessageBatch(messageBatch, stoppingToken));
                    }

                    await Task.WhenAll(tasks);
                
                    await session.CommitTransactionAsync(stoppingToken);
                
                    tasks.Clear();
                }
            }
            finally
            {
                await Task.Delay(_options.Value.Delay, stoppingToken);
            }
            
            break;
        }
    }

    private async Task SendMessageBatch(ServiceBusMessageBatch messageBatch, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity("Sender-Message-Batch");
        
        await _sender.SendMessagesAsync(messageBatch, stoppingToken);
        messageBatch.Dispose();
    }
}