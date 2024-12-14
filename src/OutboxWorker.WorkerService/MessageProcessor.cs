using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using OutboxWorker.WorkerService.Configurations;
using OutboxWorker.WorkerService.Extensions;

namespace OutboxWorker.WorkerService;

public class MessageProcessor : IMessageProcessor
{
    private readonly IOptions<OutboxOptions> _options;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<MessageRelayWorker> _logger;
    private readonly ServiceBusSender _sender;
    private readonly IMessageRepository _messageRepository;
    private readonly CreateMessageBatchOptions _batchOptions;
    private readonly OutboxMetrics _metrics;
    private readonly ParallelOptions _parallelOptions;
    private readonly int _sliceSize;
    public MessageProcessor(IOptions<OutboxOptions> options, IMessageRepository repository, ServiceBusClient busClient,
        ActivitySource activitySource, ILogger<MessageRelayWorker> logger, OutboxMetrics metrics)
    {
        _options = options;
        _logger = logger;
        _activitySource = activitySource;
        _messageRepository = repository;
        _metrics = metrics;
        
        _sender = busClient.CreateSender(_options.Value.BrokerOptions.EntityName);
        
        _batchOptions = new CreateMessageBatchOptions()
        {
            MaxSizeInBytes = 262144
        };

        _parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = _options.Value.MaxDegreeOfParallelism
        };
        
        _sliceSize = _options.Value.SliceSize;
    }
    
    public async Task InitAsync(CancellationToken cancellationToken)
    {
        _parallelOptions.CancellationToken = cancellationToken;
        
        using var messageBatch = await _sender.CreateMessageBatchAsync(_batchOptions, cancellationToken);
    }
    
    public async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        await _messageRepository.StartTransactionAsync(cancellationToken);

        try
        {
           var messages = await _messageRepository.FindMessagesAsync(cancellationToken);
           
           var slices = messages.SliceInMemory(_sliceSize);

           await Parallel.ForEachAsync(slices, _parallelOptions, async (memory, token) => await ProcessSliceAsync(memory, token));
        }
        finally
        {
           await _messageRepository.CommitTransactionAsync(cancellationToken);
        }
    }
    
    private async Task ProcessSliceAsync(ReadOnlyMemory<RawBsonDocument> messages, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity();
        var count = 0;
        var t = 0u;
        var taskCount = (messages.Length + _options.Value.BrokerOptions.BatchSize - 1) / _options.Value.BrokerOptions.BatchSize;
        var tasks = new Task[taskCount];
        var batchSize = _options.Value.BrokerOptions.BatchSize;
        
        var messageBatch = await _sender.CreateMessageBatchAsync(_batchOptions, cancellationToken);
        
        for (var i = 0; i < messages.Length; i++)
        {
            var message = messages.Span[i].ToServiceBusMessage();
            
            if (count < batchSize && messageBatch.TryAddMessage(message))
            {
                count++;
                continue;
            }

            tasks[t++] = SendMessageBatchAsync(messageBatch, cancellationToken);
            messageBatch = await _sender.CreateMessageBatchAsync(_batchOptions, cancellationToken);
            count = 0;
        }
        
        tasks[t] = SendMessageBatchAsync(messageBatch, cancellationToken);

        await Task.WhenAll(tasks);
    }
    
    private async Task SendMessageBatchAsync(ServiceBusMessageBatch messageBatch, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity();
        //await _sender.SendMessagesAsync(messageBatch, cancellationToken);
        await Task.Delay(300, cancellationToken);
        messageBatch.Dispose();
        _metrics.IncrementMessageCount(messageBatch.Count);
    }
}