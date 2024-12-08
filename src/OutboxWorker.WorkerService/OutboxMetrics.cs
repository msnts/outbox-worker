using System.Diagnostics.Metrics;

namespace OutboxWorker.WorkerService;

public class OutboxMetrics
{
    public const string MeterName = "Outbox.Worker";
    private readonly Counter<int> _messageCounter;
    private readonly Counter<int> _failedMessages;
    private readonly Histogram<int> _processingTime;

    public OutboxMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _messageCounter = meter.CreateCounter<int>("outbox.message_published.count");
        _failedMessages = meter.CreateCounter<int>("outbox.failed_messages.count");
        _processingTime = meter.CreateHistogram<int>("outbox.processing_time");
    }

    public void IncrementMessageCount(int delta = 1) => _messageCounter.Add(delta);
}