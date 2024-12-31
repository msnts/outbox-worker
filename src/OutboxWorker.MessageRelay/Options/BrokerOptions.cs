using System.ComponentModel.DataAnnotations;

namespace OutboxWorker.MessageRelay.Options;

public class BrokerOptions
{
    [Length(6, 64)]
    public string EntityName  { get; set; }
    
    [Range(1, 500)]
    public int BatchSize { get; set; }
    [Range(1, 32)]
    public int SenderCount { get; set; }
}