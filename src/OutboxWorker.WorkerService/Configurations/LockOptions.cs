namespace OutboxWorker.WorkerService.Configurations;

public class LockOptions
{
    public int Lifetime { get; set; }
    public int Timeout { get; set; }
}