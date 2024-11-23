namespace OutboxWorker.WorkerService.Configurations;

public class OutboxOptions
{
    public MongoOptions MongoOptions { get; set; }
    public int Delay { get; set; }
    public int BatchSize { get; set; }
    public int MaxDegreeOfParallelism { get; set; }
}