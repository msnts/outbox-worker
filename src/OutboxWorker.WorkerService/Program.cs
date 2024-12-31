using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using OutboxWorker.MessageRelay.Configurations;
using OutboxWorker.ServiceDefaults;
using OutboxWorker.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Logging.ClearProviders();
builder.Logging. AddConsole();

using var loggerFactory = LoggerFactory.Create(b =>
{
    b.AddSimpleConsole();
    b.SetMinimumLevel(LogLevel.Debug);
});

builder.AddMongoDBClient("mongodb", configureClientSettings: settings =>
{
    //settings.LoggingSettings = new LoggingSettings(loggerFactory);
});
builder.AddAzureServiceBusClient("messaging");

//builder.Services.AddHostedService<MessageGenerateWorker>();

#pragma warning disable 618
BsonDefaults.GuidRepresentation = GuidRepresentation.Standard;
BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

BsonClassMap.RegisterClassMap<OutboxMessage>(classMap =>
{
    classMap.AutoMap();
    classMap.SetIgnoreExtraElements(true);
});

BsonClassMap.RegisterClassMap<User>(classMap =>
{
    classMap.AutoMap();
    classMap.SetIgnoreExtraElements(true);
});

builder.Services.AddOutbox();

var host = builder.Build();
host.Run();