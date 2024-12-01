namespace OutboxWorker.WorkerService.Configurations;

public class MongoOptions
{
    public string DatabaseName { get; set; }
    public int Limit { get; set; }
    public int BatchSize { get; set; }
}