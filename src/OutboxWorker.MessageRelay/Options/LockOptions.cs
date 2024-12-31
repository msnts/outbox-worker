using System.ComponentModel.DataAnnotations;

namespace OutboxWorker.WorkerService.Configurations;

public class LockOptions
{
    [Range(10, 60)]
    public int Lifetime { get; set; }
    
    [Range(1, 30)]
    public int Timeout { get; set; }
}