using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OutboxWorker.MessageRelay.Metrics;
using OutboxWorker.MessageRelay.Options;
using OutboxWorker.MessageRelay.Processor;

namespace OutboxWorker.MessageRelay;

public class MessageRelayWorker : BackgroundService
{
    private readonly IOptions<OutboxOptions> _options;
    private readonly IMessageProcessor _messageProcessor;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<MessageRelayWorker> _logger;
    private readonly OutboxMetrics _metrics;
    
    public MessageRelayWorker(IOptions<OutboxOptions> options, IMessageProcessor messageProcessor,
        ActivitySource activitySource, ILogger<MessageRelayWorker> logger, OutboxMetrics metrics)
    {
        _options = options;
        _messageProcessor = messageProcessor;
        _logger = logger;
        _activitySource = activitySource;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stopwatch = new Stopwatch();

        await _messageProcessor.InitAsync(stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            stopwatch.Start();
            
            using var activity = _activitySource.StartActivity();

            try
            {
                await _messageProcessor.ProcessMessagesAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message, e);
            }
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
        _metrics.RecordProcessingTime(stopwatch.ElapsedMilliseconds);
        var remainingDelay = _options.Value.Delay - (int)stopwatch.ElapsedMilliseconds;
        if (remainingDelay > 0)
        {
            await Task.Delay(remainingDelay, stoppingToken);
        }
    }
}