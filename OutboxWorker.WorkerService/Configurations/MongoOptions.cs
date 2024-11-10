namespace OutboxWorker.WorkerService.Configurations;

public class MongoOptions
{
    public int Limit { get; set; }
    public int BatchSize { get; set; }
}