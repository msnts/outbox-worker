using Bogus;
using Bogus.DataSets;
using MongoDB.Bson;
using MongoDB.Driver;

namespace OutboxWorker.WorkerService;

public class MessageGenerateWorker : BackgroundService
{
    private readonly IMongoCollection<OutboxMessage> _outboxMessages;
    
    public MessageGenerateWorker(IMongoClient mongoClient)
    {
        var database = mongoClient.GetDatabase("OutboxWorkerService");
        
        database.CreateCollection("OutboxMessage", new CreateCollectionOptions()
        {
            StorageEngine = new BsonDocument
            {
                { "wiredTiger", new BsonDocument
                    {
                        { "configString", "block_compressor=zstd" }
                    }
                }
            }
        });

        _outboxMessages = database.GetCollection<OutboxMessage>("OutboxMessage");
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var usersGenerator = new Faker<User>()
            .RuleFor(u => u.Id, f => Guid.NewGuid())
            .RuleFor(u => u.Gender, f => f.PickRandom<Name.Gender>())
            .RuleFor(u => u.FirstName, (f, u) => f.Name.FirstName(u.Gender))
            .RuleFor(u => u.LastName, (f, u) => f.Name.LastName(u.Gender))
            .RuleFor(u => u.Avatar, f => f.Internet.Avatar())
            .RuleFor(u => u.UserName, (f, u) => f.Internet.UserName(u.FirstName, u.LastName))
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName))
            .RuleFor(u => u.CartId, f => Guid.NewGuid())
            .RuleFor(u => u.FullName, (f, u) => u.FirstName + " " + u.LastName);

        var users = usersGenerator.Generate(10_000);
        
        var messages = new List<OutboxMessage>(10_000);

        for (var i = 0; i < 10_000; i++)
        {
            messages.Add(new OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                CorrelationId = Guid.NewGuid(),
                Subject = "UserCreated",
                Body = users[i]
            });
        }

        return _outboxMessages.InsertManyAsync(messages, cancellationToken:stoppingToken);
    }
}