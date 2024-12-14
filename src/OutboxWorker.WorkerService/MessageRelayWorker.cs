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
    private readonly IMessageRepository _messageRepository;
    private readonly CreateMessageBatchOptions _batchOptions;
    private readonly OutboxMetrics _metrics;
    private readonly ParallelOptions _parallelOptions;
    private readonly int _sliceSize;

    public MessageRelayWorker(IOptions<OutboxOptions> options, IMessageRepository repository, ServiceBusClient busClient,
        ActivitySource activitySource, ILogger<MessageRelayWorker> logger, OutboxMetrics metrics)
    {
        _options = options;
        _logger = logger;
        _activitySource = activitySource;
        _messageRepository = repository;
        _serviceBusClient = busClient;
        _metrics = metrics;
        
        _sender = _serviceBusClient.CreateSender(_options.Value.BrokerOptions.EntityName);
        
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
                await using var distributedLock = await _messageRepository.AcquireLockAsync(stoppingToken);
                
                if (distributedLock.Acquired)
                {
                    await DoExecuteAsync(stoppingToken);
                }
            }
            //todo: tratar as exceptions
            finally
            {
                await HandleDelay(stopwatch, stoppingToken);
            }
            
            break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task HandleDelay(Stopwatch stopwatch, CancellationToken stoppingToken)
    {
        stopwatch.Stop();
        var remainingDelay = _options.Value.Delay - (int)stopwatch.ElapsedMilliseconds;
        if (remainingDelay > 0)
        {
            await Task.Delay(remainingDelay, stoppingToken);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task DoExecuteAsync(CancellationToken stoppingToken)
    {
        await _messageRepository.StartTransactionAsync(stoppingToken);

        try
        {
            var messages = await _messageRepository.FindMessagesAsync(stoppingToken);
            
            var slices = messages.SliceInMemory(_sliceSize);

            await Parallel.ForEachAsync(slices, stoppingToken, async (memory, token) => await ProcessSliceAsync(memory, token));
        }
        finally
        {
            await _messageRepository.AbortTransactionAsync(stoppingToken);
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
}