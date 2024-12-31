using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using OutboxWorker.WorkerService.Configurations;

namespace OutboxWorker.MessageRelay.Options;

public class OutboxOptions
{
    [Required, ValidateObjectMembers]
    public MongoOptions MongoOptions { get; set; }
    
    [Required, ValidateObjectMembers]
    public BrokerOptions BrokerOptions { get; set; }
    
    [Required, ValidateObjectMembers]
    public LockOptions LockOptions { get; set; }
    
    [Range(1_000, 10_000)]
    public int Delay { get; set; }
    
    [Range(1, 32)]
    public int MaxDegreeOfParallelism { get; set; }

    public int SliceSize => MongoOptions.Limit / MaxDegreeOfParallelism;
}