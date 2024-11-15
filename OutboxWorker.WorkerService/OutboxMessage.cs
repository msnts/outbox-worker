using MongoDB.Bson;

namespace WorkerService1;

public class OutboxMessage
{
    public string Id { get; set; }
    public string Body { get; set; }
}