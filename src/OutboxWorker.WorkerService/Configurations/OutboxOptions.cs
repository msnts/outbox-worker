using System.ComponentModel.DataAnnotations;

namespace OutboxWorker.WorkerService.Configurations;

public class OutboxOptions
{
    [Required]
    public MongoOptions MongoOptions { get; set; }
    [Required]
    public BrokerOptions BrokerOptions { get; set; }
    [Range(1_000,10_000)]
    public int Delay { get; set; }
    [Range(1,8)]
    public int MaxDegreeOfParallelism { get; set; }

    public int SliceSize => MongoOptions.Limit / MaxDegreeOfParallelism;
}