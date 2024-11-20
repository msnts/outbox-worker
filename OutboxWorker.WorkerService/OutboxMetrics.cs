using System.Diagnostics.Metrics;

namespace OutboxWorker.WorkerService;

public class OutboxMetrics
{
    public const string MeterName = "Outbox.Worker";
    private readonly Counter<int> _messageCounter;

    public OutboxMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _messageCounter = meter.CreateCounter<int>("outbox.message_published.count");

    }

    public void IncrementMessageCount(int delta = 1) => _messageCounter.Add(delta);
}