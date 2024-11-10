using MongoDB.Bson;

namespace WorkerService1;

public class OutboxMessage
{
    public ObjectId Id { get; set; }
    public string Payload { get; set; }
}