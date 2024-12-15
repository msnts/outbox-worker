using Bogus;
using Bogus.DataSets;
using MongoDB.Bson;
using MongoDB.Driver;

namespace OutboxWorker.WorkerService;

public class MessageGenerateWorker : BackgroundService
{
    private readonly IMongoCollection<OutboxMessage> _outboxMessages;
    private readonly Faker<User> _userGenerator;
    
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
        
        _userGenerator = new Faker<User>()
            .RuleFor(u => u.Id, f => Guid.NewGuid())
            .RuleFor(u => u.Gender, f => f.PickRandom<Name.Gender>())
            .RuleFor(u => u.FirstName, (f, u) => f.Name.FirstName(u.Gender))
            .RuleFor(u => u.LastName, (f, u) => f.Name.LastName(u.Gender))
            .RuleFor(u => u.Avatar, f => f.Internet.Avatar())
            .RuleFor(u => u.UserName, (f, u) => f.Internet.UserName(u.FirstName, u.LastName))
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName))
            .RuleFor(u => u.CartId, f => Guid.NewGuid())
            .RuleFor(u => u.FullName, (f, u) => u.FirstName + " " + u.LastName);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var users = _userGenerator.Generate(1000);
        
            var messages = new List<OutboxMessage>(1000);

            for (var i = 0; i < 100; i++)
            {
                messages.Add(new OutboxMessage
                {
                    Id = Guid.CreateVersion7(),
                    CorrelationId = Guid.NewGuid(),
                    Subject = "UserCreated",
                    Body = users[i]
                });
            }

            await _outboxMessages.InsertManyAsync(messages, cancellationToken:stoppingToken);
            
            await Task.Delay(500, stoppingToken);
        }
    }
}