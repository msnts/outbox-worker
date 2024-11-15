using MongoDB.Driver;

namespace WorkerService1;

public class MessageGenerateWorker : BackgroundService
{
    public MessageGenerateWorker()
    {
        
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mongoClient = new MongoClient("***REMOVED***");
        
        var database = mongoClient.GetDatabase("OutboxWorkerService");

        var collection = database.GetCollection<OutboxMessage>("OutboxMessage");

        var messages = new List<OutboxMessage>(1000);

        for (int i = 0; i < 1000; i++)
        {
            messages.Add(new() { Body = "Message"});
        }

        return collection.InsertManyAsync(messages);
    }
}