using System.ComponentModel.DataAnnotations;

namespace OutboxWorker.WorkerService.Configurations;

public class MongoOptions
{
    
    [Length(6, 64)]
    public string DatabaseName { get; set; }
    [Range(100, 10_000)]
    public int Limit { get; set; }
    [Range(100, 10_000)]
    public int BatchSize { get; set; }
}