using BenchmarkDotNet.Attributes;
using Bogus;
using Bogus.DataSets;
using MongoDB.Bson;
using OutboxWorker.WorkerService;
using OutboxWorker.WorkerService.Extensions;

namespace OutboxWorker.Benchmark;

public class ServiceBusMessageConverter
{
    private readonly RawBsonDocument _document;

    public ServiceBusMessageConverter()
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


        var message = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            CorrelationId = Guid.NewGuid(),
            Subject = "UserCreated",
            Body = usersGenerator.Generate(1)[0]
        };
        
        _document = new RawBsonDocument(message.ToBson());
    }
    
    [Benchmark]
    public void Converter()
    {
        _document.ToServiceBusMessage();
    }
}