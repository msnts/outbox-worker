namespace OutboxWorker.WorkerService.Configurations;

public class BrokerOptions
{
    public string EntityName  { get; set; }
    public int BatchSize { get; set; }
}